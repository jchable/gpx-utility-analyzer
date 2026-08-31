using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Anomaly.Detectors;
using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Tests.Anomaly;

public class AnomalyDetectorTests
{
    private static List<TrackPoint> FivePoints(double ele)
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int i = 0; i < 5; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.0001,
                Lon = 2.0,
                Ele = ele + i,
                Time = t0.AddSeconds(i * 5),
                DistFromPrev = i == 0 ? 0 : 11,
            });
        return points;
    }

    // #104 — DetectRaw reported the transition as [i-1, i] and
    // AnomalyCorrector.CorrectElevationSpike interpolates every index in that
    // inclusive range, so the healthy point BEFORE the spike was rewritten.
    [Fact]
    public void DetectRaw_SinglePointSpike_FlagsOnlyTheSpikingPoint()
    {
        var points = FivePoints(100);
        var raw = new List<double> { 100, 900, 102, 103, 104 };  // spike at index 1

        var anomalies = ElevationAnomalyDetector.DetectRaw(points, raw, AnomalyConfig.Default());

        Assert.NotEmpty(anomalies);
        foreach (var a in anomalies)
        {
            // CorrectElevationSpike rewrites every index in the inclusive range,
            // so a range spanning more than one point destroys good data.
            Assert.Equal(a.StartIndex, a.EndIndex);
            // Point 0 is healthy and must never be inside a reported range.
            Assert.NotEqual(0, a.StartIndex);
        }

        // The spiking point itself must still be reported.
        Assert.Contains(anomalies, a => a.StartIndex == 1);
    }

    [Fact]
    public void Detect_WithDemCorrectionApplied_DoesNotEmitRawElevationSpikes()
    {
        var points = FivePoints(512);   // accurate SRTM values
        var raw = new List<double> { 100, 900, 102, 103, 104 };

        var report = AnomalyDetector.Detect(points, [], 7.0, 44, 0,
            hasDemCorrection: true, rawElevations: raw, cfg: AnomalyConfig.Default());

        Assert.DoesNotContain(report.Anomalies, a => a.Type == AnomalyType.ElevationSpike);
    }

    // Negative control for the gate above: without DEM correction the raw spike
    // must still be reported, so the gate cannot degenerate into disabling it.
    [Fact]
    public void Detect_WithoutDemCorrection_StillEmitsRawElevationSpikes()
    {
        var points = FivePoints(100);
        var raw = new List<double> { 100, 900, 102, 103, 104 };

        var report = AnomalyDetector.Detect(points, [], 7.0, 44, 0,
            hasDemCorrection: false, rawElevations: raw, cfg: AnomalyConfig.Default());

        Assert.Contains(report.Anomalies, a => a.Type == AnomalyType.ElevationSpike);
    }
}
