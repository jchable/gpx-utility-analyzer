using System.Globalization;
using System.Xml.Linq;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Gpx;

/// <summary>
/// Issue #144. The per-point <c>gpxa:dist</c> extension is the cumulative distance the rest
/// of the stack reads back - the API's km splits and best efforts are built on nothing else.
/// It therefore has to accumulate the SAME segments the stats pipeline counts into
/// <c>total_distance_m</c>, not raw point-to-point Haversine.
///
/// The pipeline drops a segment from the total in two cases, both expressed by
/// <see cref="TrackPoint.BreaksPath"/>: there is no measurable path (a structural
/// &lt;trkseg&gt; boundary or a recording gap - the recorder was off, so whatever happened in
/// between was not recorded), or the distance is discredited (a speed clamp - the fixes are
/// too far apart in too little time to be believed). <see cref="TrackPoint.BreaksRecordedTime"/>
/// is the wrong predicate here: it deliberately excludes the clamp, because an implausible
/// speed discredits the distance between two fixes but not the seconds that elapsed between
/// them. Distance is governed by BreaksPath.
/// </summary>
public class GpxWriterCumulativeDistanceTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private const double MetresPerDegLat = 111132.0;

    // ------------------------------------------------------------------ direct, hand-flagged

    [Fact]
    public void WriteEnriched_RecordingGap_DoesNotBankTheDistanceAcrossIt()
    {
        var points = Straight(4, stepMetres: 100, stepSeconds: 10);
        points[2].AfterRecordingGap = true;   // the recorder was off across this interval

        Assert.Equal(200, LastCumDist(points), 0);
    }

    [Fact]
    public void WriteEnriched_SegmentBoundary_DoesNotBankTheDistanceAcrossIt()
    {
        var points = Straight(4, stepMetres: 100, stepSeconds: 10);
        points[2].StartsNewSegment = true;    // the source opened a new <trkseg> here

        Assert.Equal(200, LastCumDist(points), 0);
    }

    [Fact]
    public void WriteEnriched_SpeedClamp_DoesNotBankTheDiscreditedDistance()
    {
        var points = Straight(4, stepMetres: 100, stepSeconds: 10);
        points[2].SpeedClamped = true;        // the distance between these two fixes is junk

        Assert.Equal(200, LastCumDist(points), 0);
    }

    [Fact]
    public void WriteEnriched_CleanTrack_StillAccumulatesEverySegment()
    {
        var points = Straight(4, stepMetres: 100, stepSeconds: 10);

        Assert.Equal(300, LastCumDist(points), 0);
    }

    // ------------------------------------------------------------- the whole pipeline, once

    /// <summary>
    /// The contract, end to end: run a track through <see cref="ComputePipeline"/>, export it
    /// with --enrich, and the last <c>gpxa:dist</c> must be the <c>total_distance_m</c> the
    /// same run reported.
    /// </summary>
    [Fact]
    public void WriteEnriched_AfterCompute_FinalCumDistEqualsTheReportedTotalDistance()
    {
        var (summary, processed) = RunPipeline();

        // The fixture has to actually exercise both exclusions or it proves nothing.
        Assert.Contains(processed, p => p.AfterRecordingGap);
        Assert.Contains(processed, p => p.SpeedClamped);

        Assert.Equal(summary.TotalDistance, LastCumDist(processed), 6);
    }

    /// <summary>
    /// The other half of the agreement, and the commoner one: a &lt;trkseg&gt; break too short
    /// to read as a recording gap. Auto-pause and manual pause produce these constantly.
    ///
    /// Distance was the only statistic that ignored a structural boundary - elevation sections
    /// and stop runs split on BreaksPath, recorded time skips on BreaksRecordedTime, but
    /// EnrichPoints looked only at the time delta - so a 2 minute pause banked the straight
    /// line across it into total_distance_m (333.6 m for a track with 222.4 m of measured
    /// path). Fixing only the writer would have swapped an overstatement for an
    /// understatement; both sides now exclude the hop.
    /// </summary>
    [Fact]
    public void Compute_ShortTrksegBreak_ExcludesTheHopFromBothTheTotalAndTheCumDist()
    {
        const string gpx = """
            <?xml version="1.0" encoding="utf-8"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1" creator="test">
              <trk><name>t</name>
                <trkseg>
                  <trkpt lat="45.0000" lon="6.0"><ele>100</ele><time>2024-01-01T10:00:00Z</time></trkpt>
                  <trkpt lat="45.0010" lon="6.0"><ele>100</ele><time>2024-01-01T10:01:00Z</time></trkpt>
                </trkseg>
                <trkseg>
                  <trkpt lat="45.0020" lon="6.0"><ele>100</ele><time>2024-01-01T10:03:00Z</time></trkpt>
                  <trkpt lat="45.0030" lon="6.0"><ele>100</ele><time>2024-01-01T10:04:00Z</time></trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(gpx));
        var doc = GpxParser.Parse(stream);
        var cfg = new ComputeConfig
        {
            SmoothingLevel = "none", TrackSmoothing = "none",
            StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
            MaxReasonableSpeed = 8.0,
        };
        var (s, pts) = ComputePipeline.Compute(doc.AllPoints(), doc.SegmentCount(), cfg);

        // Three 0.001 deg hops of ~111.2 m each, but the middle one crosses the break.
        var oneHop = DistanceCalculator.Haversine(45.0000, 6.0, 45.0010, 6.0);
        Assert.Equal(2 * oneHop, s.TotalDistance, 6);
        Assert.Equal(s.TotalDistance, LastCumDist(pts), 6);
    }

    // ------------------------------------------------------------------------------ fixture

    /// <summary>
    /// A 3 m/s run that a GPS receiver mangles twice, in the two ways the pipeline knows how
    /// to discount:
    ///
    /// <list type="bullet">
    /// <item>a 700 m lateral teleport that persists - the outlier filter re-anchors past it
    /// and the clamp then voids the 710 m step it left behind;</item>
    /// <item>a 20 minute pause during which the athlete moved 2 km - a recording gap.</item>
    /// </list>
    ///
    /// Raw Haversine banks 710 m + 2000 m the runner never ran.
    /// </summary>
    internal static (Summary Summary, List<TrackPoint> Points) RunPipeline()
    {
        var points = new List<TrackPoint>();
        var t = T0;
        double north = 0;

        void Leg(int steps, double lonOffsetMetres)
        {
            for (var i = 0; i < steps; i++)
            {
                points.Add(NewPoint(north, lonOffsetMetres, t));
                north += 30;                       // 30 m per 10 s = 3 m/s
                t = t.AddSeconds(10);
            }
        }

        Leg(21, 0);                                // 600 m of clean running
        Leg(21, 700);                              // teleported 700 m sideways, still running
        north += 2000; t = t.AddMinutes(20);       // paused, and moved 2 km while paused
        Leg(21, 700);                              // 600 m more

        var cfg = new ComputeConfig
        {
            ElevationThreshold = 2.0,
            SmoothingLevel = "none",
            TrackSmoothing = "none",
            StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
            MaxReasonableSpeed = 8.0,
        };

        return ComputePipeline.Compute(points, 1, cfg);
    }

    private static TrackPoint NewPoint(double northMetres, double eastMetres, DateTime time)
    {
        const double lat = 45.0;
        var metresPerDegLon = 111320.0 * Math.Cos(lat * Math.PI / 180.0);
        return new TrackPoint
        {
            Lat = lat + northMetres / MetresPerDegLat,
            Lon = 6.0 + eastMetres / metresPerDegLon,
            Ele = 100,
            Time = time,
        };
    }

    private static List<TrackPoint> Straight(int count, double stepMetres, int stepSeconds) =>
        [.. Enumerable.Range(0, count)
            .Select(i => NewPoint(i * stepMetres, 0, T0.AddSeconds(i * stepSeconds)))];

    /// <summary>Writes the points with --enrich and reads the last gpxa:dist back out.</summary>
    internal static double LastCumDist(List<TrackPoint> points)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cumdist_{Guid.NewGuid():N}.gpx");
        try
        {
            GpxWriter.WriteEnriched(path, points, "t");
            return ReadCumDists(path)[^1];
        }
        finally { File.Delete(path); }
    }

    internal static List<double> ReadCumDists(string path)
    {
        XNamespace gpxa = "http://gpx-analyzer.io/extensions/v1";
        return [.. XDocument.Load(path)
            .Descendants(gpxa + "dist")
            .Select(e => double.Parse(e.Value, CultureInfo.InvariantCulture))];
    }
}
