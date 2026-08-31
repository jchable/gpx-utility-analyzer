using GpxAnalyzer.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GpxAnalyzer.Api.Tests.Profile;

/// <summary>
/// Unit tests for ProfileComputationService's enriched-GPX parser. These are pure
/// parsing tests — no host, no database — so they stay out of the "Integration"
/// collection.
/// </summary>
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
}
