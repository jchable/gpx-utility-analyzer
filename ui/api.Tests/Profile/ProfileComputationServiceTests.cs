using System.Globalization;
using System.Text;
using System.Text.Json;
using GpxAnalyzer.Api.Services;
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
}
