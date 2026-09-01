using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;
using GpxAnalyzer.Cli.Tests.Characterization;

namespace GpxAnalyzer.Cli.Tests.Gpx;

/// <summary>
/// Statistics are segment-dependent: elevation sections, stop runs and recorded time all break
/// at a &lt;trkseg&gt; boundary. A GPX this tool writes must therefore carry its boundaries, or
/// every number changes the moment the file is read back - which is exactly what `merge
/// --analyze`, `analyze --export` and `split` hand their users.
/// </summary>
public class GpxSegmentRoundTripTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Two recording segments one minute apart. Each climbs 20 m over two minutes; the 30 m
    /// step across the boundary is not climbed. Boundary honoured: +40 m over 4 minutes.
    /// Boundary lost: +70 m over 5.
    /// </summary>
    private static List<TrackPoint> TwoSegmentTrack()
    {
        double[] elevations = [100, 110, 120, 150, 160, 170];
        return [.. elevations.Select((ele, i) => new TrackPoint
        {
            Lat = 45.0 + i * 0.001,
            Lon = 6.0,
            Ele = ele,
            Time = T0.AddMinutes(i),
            StartsNewSegment = i == 3,
        })];
    }

    private static ComputeConfig NoSmoothing() => new()
    {
        ElevationThreshold = 2.0,
        SmoothingLevel = "none",
        StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
        MaxReasonableSpeed = SpeedCalculator.PresetMaxSpeed[StopDetector.PresetHiking],
    };

    private static string TempGpx() =>
        Path.Combine(Path.GetTempPath(), $"gpxa-roundtrip-{Guid.NewGuid():N}.gpx");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_TrackWithASegmentBoundary_EmitsOneTrksegPerSegment(bool enrich)
    {
        var path = TempGpx();
        try
        {
            var points = TwoSegmentTrack();
            if (enrich)
                GpxWriter.WriteEnriched(path, points, "round-trip");
            else
                GpxWriter.Write(path, points, "round-trip");

            var xml = File.ReadAllText(path);
            Assert.Equal(2, xml.Split("<trkseg>").Length - 1);
            Assert.Equal(2, xml.Split("</trkseg>").Length - 1);

            var reread = GpxParser.ParseFile(path);
            Assert.Equal(2, reread.SegmentCount());
            Assert.Equal([false, false, false, true, false, false],
                reread.AllPoints().Select(p => p.StartsNewSegment));
        }
        finally { File.Delete(path); }
    }

    /// <summary>The point of the exercise: the numbers have to survive the file.</summary>
    [Fact]
    public void WriteThenReadBack_ProducesTheSameStatistics()
    {
        var path = TempGpx();
        try
        {
            var (before, _) = ComputePipeline.Compute(TwoSegmentTrack(), 2, NoSmoothing());
            GpxWriter.Write(path, TwoSegmentTrack(), "round-trip");

            var doc = GpxParser.ParseFile(path);
            var (after, _) = ComputePipeline.Compute(doc.AllPoints(), doc.SegmentCount(), NoSmoothing());

            Assert.Equal(40, before.Elevation.Gain, 6);          // the boundary is doing work
            Assert.Equal(before.Elevation.Gain, after.Elevation.Gain, 6);
            Assert.Equal(before.Elevation.Loss, after.Elevation.Loss, 6);
            Assert.Equal(before.MovingTime, after.MovingTime);
            Assert.Equal(before.TotalDistance, after.TotalDistance, 6);
            Assert.Equal(before.SegmentCount, after.SegmentCount);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// A recording gap is what a &lt;trkseg&gt; break means in GPX, so a processed track whose
    /// gaps the pipeline discovered has to write them out as breaks too.
    /// </summary>
    [Fact]
    public void Write_TrackWithARecordingGap_EmitsATrksegBreakThere()
    {
        var path = TempGpx();
        try
        {
            var points = new List<TrackPoint>
            {
                new() { Lat = 45.000, Lon = 6.0, Ele = 100, Time = T0 },
                new() { Lat = 45.001, Lon = 6.0, Ele = 110, Time = T0.AddMinutes(1) },
                new() { Lat = 45.002, Lon = 6.0, Ele = 120, Time = T0.AddMinutes(45) },
                new() { Lat = 45.003, Lon = 6.0, Ele = 130, Time = T0.AddMinutes(46) },
            };
            SpeedCalculator.EnrichPoints(points);
            Assert.True(points[2].AfterRecordingGap);   // arrange

            GpxWriter.Write(path, points, "gapped");

            Assert.Equal(2, GpxParser.ParseFile(path).SegmentCount());
        }
        finally { File.Delete(path); }
    }

    // ------------------------------------------------------------------- through the CLI

    /// <summary>
    /// End-to-end: `merge --analyze` printed D+ 40 m and then `analyze` on the file it had just
    /// written printed D+ 70 m, because the merged output was a single flattened &lt;trkseg&gt;.
    /// </summary>
    [Fact]
    public void Merge_ThenAnalyzeTheMergedFile_ReportsTheSameElevationGain()
    {
        var carried = TempGpx();
        try
        {
            var merged = CliRunner.Run(
                new CliOptions { Inspect = w => File.Copy(Path.Combine(w, "out", "merged.gpx"), carried) },
                "--format", "json", "merge", "close-segments.gpx",
                "--output", "out/merged.gpx", "--analyze",
                "--smoothing", "none", "--dem-auto-download", "false");
            Assert.Equal(0, merged.ExitCode);

            var reanalyzed = CliRunner.Run(
                new CliOptions { Arrange = w => File.Copy(carried, Path.Combine(w, "merged.gpx")) },
                "--format", "json", "analyze", "merged.gpx",
                "--smoothing", "none", "--dem-auto-download", "false");
            Assert.Equal(0, reanalyzed.ExitCode);

            Assert.Equal(40, ElevationGain(merged.StdOut), 6);
            Assert.Equal(ElevationGain(merged.StdOut), ElevationGain(reanalyzed.StdOut), 6);
        }
        finally { File.Delete(carried); }
    }

    /// <summary>The same round trip through `analyze --export`.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnalyzeExport_ThenAnalyzeTheExportedFile_ReportsTheSameElevationGain(bool enrich)
    {
        string[] args = enrich
            ? ["analyze", "close-segments.gpx", "--smoothing", "none",
               "--dem-auto-download", "false", "--export", "exported", "--enrich"]
            : ["analyze", "close-segments.gpx", "--smoothing", "none",
               "--dem-auto-download", "false", "--export", "exported"];

        var carried = TempGpx();
        try
        {
            var direct = CliRunner.Run(
                new CliOptions
                {
                    Inspect = w => File.Copy(
                        Path.Combine(w, "exported", "close-segments_processed.gpx"), carried),
                },
                [.. new[] { "--format", "json" }, .. args]);
            Assert.Equal(0, direct.ExitCode);

            var reanalyzed = CliRunner.Run(
                new CliOptions { Arrange = w => File.Copy(carried, Path.Combine(w, "processed.gpx")) },
                "--format", "json", "analyze", "processed.gpx",
                "--smoothing", "none", "--dem-auto-download", "false");
            Assert.Equal(0, reanalyzed.ExitCode);

            Assert.Equal(40, ElevationGain(direct.StdOut), 6);
            Assert.Equal(ElevationGain(direct.StdOut), ElevationGain(reanalyzed.StdOut), 6);
        }
        finally { File.Delete(carried); }
    }

    private static double ElevationGain(string json) =>
        System.Text.Json.JsonDocument.Parse(json).RootElement
            .GetProperty("elevation_gain_m").GetDouble();
}
