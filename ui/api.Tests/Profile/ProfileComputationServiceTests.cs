using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;
using Microsoft.Extensions.Logging.Abstractions;

namespace GpxAnalyzer.Api.Tests.Profile;

/// <summary>
/// Unit tests for ProfileComputationService's enriched-GPX parser and its km-split
/// arithmetic. These are pure — no host, no database — but they still join the
/// "Integration" collection: run in parallel they add just enough CPU and temp-file
/// churn to widen the worker-vs-delete file-lock race in issue #131, which took
/// StorageApiTests.Delete_Activity_ThenDownload_Returns404 from 0/10 to 5/10
/// failures in an interleaved A/B run. Serialising them costs ~40 ms.
/// </summary>
[Collection("Integration")]
public class ProfileComputationServiceTests
{
    [Fact]
    public void ComputeFromEnrichedGpx_PowerMeterFile_PopulatesThePowerSeries()
    {
        // An enriched GPX exactly as GpxWriter.WriteEnriched emits it.
        const string gpx = """
            <?xml version="1.0" encoding="utf-8"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1" creator="gpx-analyzer"
                 xmlns:gpxa="http://gpx-analyzer.io/extensions/v1"
                 xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
              <trk><name>t</name><trkseg>
                <trkpt lat="48.0" lon="2.0"><ele>100</ele><time>2024-01-02T10:04:05Z</time>
                  <extensions>
                    <gpxa:TrackPointMetrics><gpxa:speed>2.5</gpxa:speed><gpxa:dist>0</gpxa:dist><gpxa:grade>0</gpxa:grade></gpxa:TrackPointMetrics>
                    <power>250</power>
                  </extensions></trkpt>
                <trkpt lat="48.001" lon="2.0"><ele>110</ele><time>2024-01-02T10:04:35Z</time>
                  <extensions>
                    <gpxa:TrackPointMetrics><gpxa:speed>2.6</gpxa:speed><gpxa:dist>75</gpxa:dist><gpxa:grade>0.13</gpxa:grade></gpxa:TrackPointMetrics>
                    <power>260</power>
                  </extensions></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var tmp = Path.Combine(Path.GetTempPath(), $"pwr_{Guid.NewGuid():N}.gpx");
        File.WriteAllText(tmp, gpx);
        try
        {
            var svc = new ProfileComputationService(
                NullLogger<ProfileComputationService>.Instance);
            var (profileJson, _, splitsJson) = svc.ComputeFromEnrichedGpx(tmp);

            Assert.NotNull(profileJson);
            // JsonIgnoreCondition.WhenWritingNull means an absent key IS the bug.
            Assert.Contains("\"power\"", profileJson);
            Assert.Contains("250", profileJson);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void ComputeFromEnrichedGpx_PowerInNoNamespace_IsStillRead()
    {
        // A hand-written or third-party GPX whose <power> carries no namespace at
        // all. The unqualified fallback must keep working.
        const string gpx = """
            <?xml version="1.0" encoding="utf-8"?>
            <gpx version="1.1" creator="third-party"
                 xmlns:gpxa="http://gpx-analyzer.io/extensions/v1">
              <trk><name>t</name><trkseg>
                <trkpt lat="48.0" lon="2.0"><ele>100</ele><time>2024-01-02T10:04:05Z</time>
                  <extensions>
                    <gpxa:TrackPointMetrics><gpxa:speed>2.5</gpxa:speed><gpxa:dist>0</gpxa:dist><gpxa:grade>0</gpxa:grade></gpxa:TrackPointMetrics>
                    <power>250</power>
                  </extensions></trkpt>
                <trkpt lat="48.001" lon="2.0"><ele>110</ele><time>2024-01-02T10:04:35Z</time>
                  <extensions>
                    <gpxa:TrackPointMetrics><gpxa:speed>2.6</gpxa:speed><gpxa:dist>75</gpxa:dist><gpxa:grade>0.13</gpxa:grade></gpxa:TrackPointMetrics>
                    <power>260</power>
                  </extensions></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var tmp = Path.Combine(Path.GetTempPath(), $"pwr_{Guid.NewGuid():N}.gpx");
        File.WriteAllText(tmp, gpx);
        try
        {
            var svc = new ProfileComputationService(
                NullLogger<ProfileComputationService>.Instance);
            var (profileJson, _, _) = svc.ComputeFromEnrichedGpx(tmp);

            Assert.NotNull(profileJson);
            Assert.Contains("\"power\"", profileJson);
        }
        finally { File.Delete(tmp); }
    }

    // ── Km splits: boundary-segment attribution (issue #116) ──────────────────

    [Fact]
    public void ComputeFromEnrichedGpx_SustainedClimb_SplitGainSumsToTheActivityGain()
    {
        // A hike logged every 100 m on a sustained 10 % climb: every one of the
        // 100 segments carries exactly 10 m of gain, so the whole activity rises
        // 1000 m over 10.0 km and every kilometre must be worth exactly 100 m.
        const int segments = 100;
        const double metresPerPoint = 100;
        const double gainPerSegment = 10;

        double Ele(int i) => 100 + (i * gainPerSegment);

        var splits = ComputeSplitsFor(segments + 1, metresPerPoint, Ele);

        Assert.Equal(10, splits.Count);

        var summedGain = splits.Sum(s => s.GetProperty("elevationGain").GetDouble());

        // Independent oracle: on a monotone climb the whole-activity gain is just
        // the end-to-end rise — no split arithmetic involved.
        var activityGain = Ele(segments) - Ele(0);

        // Each split's gain is rounded to 0.1 m, so 10 splits accumulate at most
        // 0.5 m of rounding error.
        Assert.InRange(summedGain, activityGain - 0.5, activityGain + 0.5);

        // And the per-split value: the boundary segment must land in exactly one
        // split, not in both the one it ends and the one it starts.
        Assert.All(splits, s =>
            Assert.InRange(s.GetProperty("elevationGain").GetDouble(), 99.5, 100.5));
    }

    [Fact]
    public void ComputeFromEnrichedGpx_RollingProfile_SplitGainAndLossSumToTheActivityTotals()
    {
        // A rolling course: within each kilometre the first five 100 m segments
        // descend 10 m and the last five climb 10 m back. That puts a genuine
        // 10 m *ascent* on every km boundary, which is precisely the segment the
        // splits used to credit twice.
        const int segments = 100;
        const double metresPerPoint = 100;

        static double Ele(int i)
        {
            var phase = i % 10;
            return phase <= 5 ? 200 - (10 * phase) : 150 + (10 * (phase - 5));
        }

        var splits = ComputeSplitsFor(segments + 1, metresPerPoint, Ele);

        Assert.Equal(10, splits.Count);

        // Independent oracle: walk the fixture's own elevations once. This is the
        // whole-activity gain/loss, computed with no reference to split boundaries.
        double activityGain = 0, activityLoss = 0;
        for (var i = 1; i <= segments; i++)
        {
            var d = Ele(i) - Ele(i - 1);
            if (d > 0) activityGain += d;
            else activityLoss += Math.Abs(d);
        }

        var summedGain = splits.Sum(s => s.GetProperty("elevationGain").GetDouble());
        var summedLoss = splits.Sum(s => s.GetProperty("elevationLoss").GetDouble());

        Assert.InRange(summedGain, activityGain - 0.5, activityGain + 0.5);
        Assert.InRange(summedLoss, activityLoss - 0.5, activityLoss + 0.5);
    }

    [Fact]
    public void ComputeFromEnrichedGpx_SegmentCrossingBoundary_IsSplitProportionally()
    {
        // Three 600 m segments at 10%: the 1 km boundary lies 400 m into the
        // second segment. The 180 m total gain must be apportioned 100 m / 80 m.
        var splits = ComputeSplitsFor(4, 600, i => 100 + (i * 60));

        Assert.Equal(2, splits.Count);
        Assert.Equal(100, splits[0].GetProperty("elevationGain").GetDouble(), 1);
        Assert.Equal(80, splits[1].GetProperty("elevationGain").GetDouble(), 1);
    }

    /// <summary>
    /// Builds an enriched GPX fixture, runs it through
    /// <see cref="ProfileComputationService.ComputeFromEnrichedGpx"/> and returns
    /// the parsed <c>splits</c> array.
    /// </summary>
    private static List<JsonElement> ComputeSplitsFor(
        int pointCount, double metresPerPoint, Func<int, double> elevationAt)
    {
        var start = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var gpx = BuildEnrichedGpx(pointCount, metresPerPoint, elevationAt, start);

        var tmp = Path.Combine(Path.GetTempPath(), $"splits_{Guid.NewGuid():N}.gpx");
        File.WriteAllText(tmp, gpx);
        try
        {
            var svc = new ProfileComputationService(
                NullLogger<ProfileComputationService>.Instance);
            var (_, _, splitsJson) = svc.ComputeFromEnrichedGpx(tmp);

            Assert.NotNull(splitsJson);
            using var doc = JsonDocument.Parse(splitsJson!);
            return [.. doc.RootElement.GetProperty("splits").EnumerateArray()
                        .Select(e => e.Clone())];
        }
        finally { File.Delete(tmp); }
    }

    /// <summary>
    /// Emits an enriched GPX in the shape <c>GpxWriter.WriteEnriched</c> produces:
    /// the default GPX namespace, and a <c>gpxa:TrackPointMetrics</c> block per
    /// point carrying the cumulative <c>gpxa:dist</c> the split code reads.
    /// </summary>
    private static string BuildEnrichedGpx(
        int pointCount, double metresPerPoint, Func<int, double> elevationAt,
        DateTime start, double secondsPerPoint = 60)
    {
        var inv = CultureInfo.InvariantCulture;
        var speed = (metresPerPoint / secondsPerPoint).ToString("F3", inv);

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        sb.AppendLine("""
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1" creator="gpx-analyzer"
                 xmlns:gpxa="http://gpx-analyzer.io/extensions/v1">
            """);
        sb.AppendLine("  <trk><name>t</name><trkseg>");

        for (var i = 0; i < pointCount; i++)
        {
            var ele = elevationAt(i);
            var dist = i * metresPerPoint;
            var grade = i == 0 ? 0 : (ele - elevationAt(i - 1)) / metresPerPoint;
            // ~100 m of latitude per step, so the track is geographically plausible.
            var lat = 48.0 + (i * 0.0009);
            var time = start.AddSeconds(i * secondsPerPoint);

            sb.Append("    <trkpt lat=\"").Append(lat.ToString("F6", inv))
              .Append("\" lon=\"2.000000\"><ele>").Append(ele.ToString("F1", inv))
              .Append("</ele><time>").Append(time.ToString("yyyy-MM-ddTHH:mm:ssZ", inv))
              .Append("</time><extensions><gpxa:TrackPointMetrics>")
              .Append("<gpxa:speed>").Append(speed)
              .Append("</gpxa:speed><gpxa:dist>").Append(dist.ToString("F1", inv))
              .Append("</gpxa:dist><gpxa:grade>").Append(grade.ToString("F4", inv))
              .AppendLine("</gpxa:grade></gpxa:TrackPointMetrics></extensions></trkpt>");
        }

        sb.AppendLine("  </trkseg></trk>");
        sb.AppendLine("</gpx>");
        return sb.ToString();
    }

    // ───────────────────────────────────────────────────── issue #144, from the consumer end

    /// <summary>
    /// <c>gpxa:dist</c> is the only distance axis this service has: <c>CumDist</c> places every
    /// km-split boundary and is the ruler the best-effort sliding window measures against. When
    /// the writer banked distance the stats pipeline had discounted, the window reached its
    /// target having covered far less real ground - a "best 1 km" run at a pace the athlete
    /// never touched, over a few hundred actual metres.
    ///
    /// The fixture is a steady 3 m/s run that the receiver mangles twice: a 700 m lateral
    /// teleport (voided by the speed clamp) and a 20 minute pause covering 2 km (a recording
    /// gap). Both are distance the runner did not run, and the second one is nearly free of
    /// time, which is what makes it poison a best effort rather than merely inflate a total.
    /// </summary>
    [Fact]
    public void ComputeFromEnrichedGpx_TrackWithAGapAndAClamp_MeasuresEffortsOnRealDistance()
    {
        var (summary, processed) = BuildManglablePipelineRun();

        // The fixture must actually exercise both exclusions or it proves nothing.
        Assert.Contains(processed, p => p.AfterRecordingGap);
        Assert.Contains(processed, p => p.SpeedClamped);

        var tmp = Path.Combine(Path.GetTempPath(), $"eff_{Guid.NewGuid():N}.gpx");
        GpxWriter.WriteEnriched(tmp, processed, "t");
        try
        {
            var svc = new ProfileComputationService(
                NullLogger<ProfileComputationService>.Instance);
            var (_, _, splitsJson) = svc.ComputeFromEnrichedGpx(tmp);

            Assert.NotNull(splitsJson);
            using var doc = JsonDocument.Parse(splitsJson);

            var oneKm = doc.RootElement.GetProperty("bestEfforts")
                .EnumerateArray().Single(e => e.GetProperty("label").GetString() == "1 km");

            // The athlete never exceeded 3 m/s, so no real kilometre can be under 333 s.
            Assert.False(oneKm.GetProperty("timeSeconds").ValueKind == JsonValueKind.Null,
                "the 1 km effort should still be measurable");
            Assert.True(oneKm.GetProperty("timeSeconds").GetDouble() >= 1000.0 / 3.0,
                $"best 1 km came back as {oneKm.GetProperty("timeSeconds").GetDouble():F1} s, "
                + "faster than the fixture ever ran");

            // The distance axis the splits are laid out on is total_distance_m, so the
            // activity spans as many kilometres as the pipeline says it does.
            var expectedKm = (int)Math.Ceiling(summary.TotalDistance / 1000.0);
            var splits = doc.RootElement.GetProperty("splits");
            Assert.Equal(expectedKm, splits.GetArrayLength());
        }
        finally { File.Delete(tmp); }
    }

    /// <summary>
    /// The decision on already-stored activities was "no migration": reanalyze re-runs the CLI
    /// pipeline and rewrites the processed GPX, so an activity heals the moment it is
    /// reanalysed. This is that claim, executed rather than asserted.
    ///
    /// It starts from a file in the state the old writer left behind - correct geometry,
    /// inflated <c>gpxa:dist</c> - confirms the profile built from it really is wrong, then
    /// replays exactly what reanalyze does (parse → ComputePipeline → WriteEnriched →
    /// ComputeFromEnrichedGpx) and checks the answer that comes back.
    ///
    /// The load-bearing detail is that GpxParser reads no <c>gpxa</c> extension at all: the
    /// stale cumulative distance is not input to anything, so re-running the pipeline over the
    /// stored file recomputes it from lat/lon/time rather than inheriting it.
    /// </summary>
    [Fact]
    public void Reanalyze_ActivityStoredWithAnInflatedCumDist_ComesBackCorrected()
    {
        var svc = new ProfileComputationService(NullLogger<ProfileComputationService>.Instance);
        var (_, processed) = BuildManglablePipelineRun();

        var stored = Path.Combine(Path.GetTempPath(), $"stale_{Guid.NewGuid():N}.gpx");
        var reprocessed = Path.Combine(Path.GetTempPath(), $"fresh_{Guid.NewGuid():N}.gpx");
        try
        {
            GpxWriter.WriteEnriched(stored, processed, "t");
            RewriteCumDistAsRawHaversine(stored);   // put the file back in its historical state

            // The activity as it sits in the database today.
            var (_, _, staleSplits) = svc.ComputeFromEnrichedGpx(stored);
            Assert.NotNull(staleSplits);
            using (var staleDoc = JsonDocument.Parse(staleSplits))
            {
                var stale1Km = staleDoc.RootElement.GetProperty("bestEfforts").EnumerateArray()
                    .Single(e => e.GetProperty("label").GetString() == "1 km");
                Assert.True(stale1Km.GetProperty("timeSeconds").GetDouble() < 1000.0 / 3.0,
                    "the historical fixture is supposed to be wrong; if it is not, this test "
                    + "proves nothing about reanalyze repairing it");
            }

            // What POST /api/activities/{id}/reanalyze runs: the pipeline over the stored
            // file, the processed GPX rewritten from its output, the profile rebuilt from that.
            var doc = GpxParser.ParseFile(stored);
            var (summary, points) = ComputePipeline.Compute(doc.AllPoints(), doc.SegmentCount(), PipelineConfig());
            GpxWriter.WriteEnriched(reprocessed, points, "t");

            var (_, _, freshSplits) = svc.ComputeFromEnrichedGpx(reprocessed);
            Assert.NotNull(freshSplits);
            using var freshDoc = JsonDocument.Parse(freshSplits);

            var oneKm = freshDoc.RootElement.GetProperty("bestEfforts").EnumerateArray()
                .Single(e => e.GetProperty("label").GetString() == "1 km");
            Assert.True(oneKm.GetProperty("timeSeconds").GetDouble() >= 1000.0 / 3.0,
                $"reanalyze left the 1 km effort at {oneKm.GetProperty("timeSeconds").GetDouble():F1} s");

            Assert.Equal((int)Math.Ceiling(summary.TotalDistance / 1000.0),
                freshDoc.RootElement.GetProperty("splits").GetArrayLength());
        }
        finally
        {
            File.Delete(stored);
            File.Delete(reprocessed);
        }
    }

    /// <summary>
    /// Replaces every <c>gpxa:dist</c> with the raw point-to-point Haversine running total the
    /// writer used to emit - the shape of every enriched GPX written before issue #144.
    /// </summary>
    private static void RewriteCumDistAsRawHaversine(string path)
    {
        XNamespace gpxa = "http://gpx-analyzer.io/extensions/v1";
        var doc = XDocument.Load(path);
        var ns = doc.Root!.GetDefaultNamespace();

        double cum = 0;
        double prevLat = 0, prevLon = 0;
        var first = true;

        foreach (var trkpt in doc.Descendants(ns + "trkpt"))
        {
            var lat = double.Parse(trkpt.Attribute("lat")!.Value, CultureInfo.InvariantCulture);
            var lon = double.Parse(trkpt.Attribute("lon")!.Value, CultureInfo.InvariantCulture);
            if (!first) cum += DistanceCalculator.Haversine(prevLat, prevLon, lat, lon);
            prevLat = lat; prevLon = lon; first = false;

            trkpt.Descendants(gpxa + "dist").Single().Value =
                cum.ToString(CultureInfo.InvariantCulture);
        }

        doc.Save(path);
    }

    private static ComputeConfig PipelineConfig() => new()
    {
        ElevationThreshold = 2.0,
        SmoothingLevel = "none",
        TrackSmoothing = "none",
        StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
        MaxReasonableSpeed = 8.0,
    };

    /// <summary>
    /// A 3 m/s run with a persistent 700 m lateral teleport (the outlier filter re-anchors
    /// past it, the clamp voids the step it leaves behind) and a 20 minute pause during which
    /// the athlete moved 2 km.
    /// </summary>
    private static (Summary Summary, List<TrackPoint> Points) BuildManglablePipelineRun()
    {
        var t0 = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        const double lat0 = 45.0;
        const double metresPerDegLat = 111132.0;
        var metresPerDegLon = 111320.0 * Math.Cos(lat0 * Math.PI / 180.0);

        var points = new List<TrackPoint>();
        var t = t0;
        double north = 0;

        void Leg(int steps, double eastMetres)
        {
            for (var i = 0; i < steps; i++)
            {
                points.Add(new TrackPoint
                {
                    Lat = lat0 + north / metresPerDegLat,
                    Lon = 6.0 + eastMetres / metresPerDegLon,
                    Ele = 100,
                    Time = t,
                });
                north += 30;                  // 30 m per 10 s = 3 m/s
                t = t.AddSeconds(10);
            }
        }

        Leg(21, 0);
        Leg(21, 700);                          // teleported sideways, still running
        north += 2000; t = t.AddMinutes(20);   // paused, and moved 2 km while paused
        Leg(21, 700);

        return ComputePipeline.Compute(points, 1, PipelineConfig());
    }
}
