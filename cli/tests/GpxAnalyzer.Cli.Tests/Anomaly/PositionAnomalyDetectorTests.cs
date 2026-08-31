using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Anomaly.Detectors;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Anomaly;

public class PositionAnomalyDetectorTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Stop detection loose enough that both fixtures below produce a stop.</summary>
    private static StopConfig Cfg() => new()
    {
        MaxSpeed = 1.0,
        MinDuration = TimeSpan.FromSeconds(30),
        MaxDistance = 100,
    };

    private const double MetresPerDegLat = 111_320.0;
    private static double MetresPerDegLon(double lat) => MetresPerDegLat * Math.Cos(lat * Math.PI / 180.0);

    private static TrackPoint At(double northM, double eastM, DateTime time) => new()
    {
        Lat = 48.0 + northM / MetresPerDegLat,
        Lon = 2.0 + eastM / MetresPerDegLon(48.0),
        Ele = 100,
        Time = time,
    };

    /// <summary>
    /// A track that moves, stops recording for three hours (a paused watch), then
    /// resumes 500 m away. The stop detector correctly reports the pause as a stop.
    /// </summary>
    private static List<TrackPoint> TrackWithARecordingGap()
    {
        var points = new List<TrackPoint>();
        for (int i = 0; i < 10; i++)
            points.Add(At(i * 120, 0, T0.AddMinutes(i)));            // 2 m/s

        var resume = T0.AddMinutes(9).AddHours(3);
        for (int i = 0; i < 10; i++)
            points.Add(At(9 * 120 + 500 + i * 120, 0, resume.AddMinutes(i)));

        return points;
    }

    /// <summary>
    /// A genuinely stationary period whose fixes wander a 30 m circle around a fixed
    /// point — real receiver drift, recorded throughout.
    /// </summary>
    private static List<TrackPoint> TrackWithStationaryDrift()
    {
        var points = new List<TrackPoint> { At(0, 0, T0) };

        // Approach, so the stop has a moving segment before it.
        for (int i = 1; i <= 5; i++)
            points.Add(At(i * 20, 0, T0.AddSeconds(i * 10)));

        var stopStart = T0.AddSeconds(50);
        const double radiusM = 30.0;
        for (int i = 0; i < 60; i++)
        {
            double theta = 2 * Math.PI * i / 60.0;
            points.Add(At(100 + radiusM * Math.Cos(theta), radiusM * Math.Sin(theta),
                stopStart.AddSeconds(10 + i * 10)));
        }

        // Depart, so the stop is closed before the end of the track.
        for (int i = 1; i <= 5; i++)
            points.Add(At(100 + radiusM + i * 20, 0, stopStart.AddSeconds(610 + i * 10)));

        return points;
    }

    private static (List<Stop> Stops, List<TrackAnomaly> Anomalies) Run(List<TrackPoint> points)
    {
        SpeedCalculator.EnrichPoints(points);
        var stops = StopDetector.DetectStops(points, Cfg());
        return (stops, PositionAnomalyDetector.Detect(points, stops, AnomalyConfig.Default()));
    }

    // #138 — a recording gap is the ABSENCE of fixes; its endpoints are far apart
    // because hours passed, not because the receiver misbehaved. Since the #80 fix
    // made the stop detector report such a pause as a stop, the drift detector
    // started emitting a gps_drift advisory across every paused watch.
    [Fact]
    public void Detect_RecordingGap_DoesNotReportGpsDrift()
    {
        var (stops, anomalies) = Run(TrackWithARecordingGap());

        // The pause must still be recognised as a stop (that is the #80 behaviour).
        Assert.Contains(stops, s => s.Duration >= TimeSpan.FromHours(2));

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.GpsDrift);
    }

    // Negative control: the fix must not degenerate into disabling drift detection.
    [Fact]
    public void Detect_StationaryDriftWithNoRecordingGap_StillReportsGpsDrift()
    {
        var (stops, anomalies) = Run(TrackWithStationaryDrift());

        Assert.NotEmpty(stops);
        Assert.All(stops, s => Assert.False(s.SpansRecordingGap));

        Assert.Contains(anomalies, a => a.Type == AnomalyType.GpsDrift);
    }

    [Fact]
    public void DetectStops_RecordingGap_MarksTheStopAsSpanningOne()
    {
        var points = TrackWithARecordingGap();
        SpeedCalculator.EnrichPoints(points);

        var stops = StopDetector.DetectStops(points, Cfg());

        Assert.Contains(stops, s => s.SpansRecordingGap);
    }
}
