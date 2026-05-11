using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Anomaly.Detectors;

/// <summary>
/// Detects biometric anomalies: HR spikes and out-of-range HR values.
/// </summary>
public static class BiometricAnomalyDetector
{
    public static List<TrackAnomaly> Detect(List<TrackPoint> points, AnomalyConfig cfg)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2)
            return anomalies;

        // Only run if biometric data is present
        bool hasHr = false;
        for (int i = 0; i < Math.Min(100, points.Count); i++)
        {
            if (points[i].HeartRate.HasValue) { hasHr = true; break; }
        }

        if (!hasHr) return anomalies;

        DetectHrOutOfRange(points, cfg, anomalies);
        DetectHrSpike(points, cfg, anomalies);

        return anomalies;
    }

    private static void DetectHrOutOfRange(List<TrackPoint> points, AnomalyConfig cfg,
        List<TrackAnomaly> anomalies)
    {
        int i = 0;
        while (i < points.Count)
        {
            if (points[i].HeartRate is int hr && (hr < cfg.HrMinBpm || hr > cfg.HrMaxBpm))
            {
                int runStart = i;
                while (i < points.Count &&
                       points[i].HeartRate is int h &&
                       (h < cfg.HrMinBpm || h > cfg.HrMaxBpm))
                {
                    i++;
                }

                anomalies.Add(new TrackAnomaly
                {
                    Type = AnomalyType.HeartRateOutOfRange,
                    Severity = AnomalySeverity.Warning,
                    Category = AnomalyCategory.Biometric,
                    StartIndex = runStart,
                    EndIndex = i - 1,
                    StartTime = points[runStart].Time,
                    EndTime = points[i - 1].Time,
                    DistanceImpactM = 0,
                    TimeImpactS = 0,
                    Description = $"Heart rate out of range: {hr}bpm (valid range: {cfg.HrMinBpm}-{cfg.HrMaxBpm}bpm) for {i - runStart} points",
                });
                continue;
            }
            i++;
        }
    }

    private static void DetectHrSpike(List<TrackPoint> points, AnomalyConfig cfg,
        List<TrackAnomaly> anomalies)
    {
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].HeartRate is int hr && points[i - 1].HeartRate is int prevHr)
            {
                int diff = Math.Abs(hr - prevHr);
                if (diff > cfg.HrSpikeThresholdBpm)
                {
                    anomalies.Add(new TrackAnomaly
                    {
                        Type = AnomalyType.HeartRateSpike,
                        Severity = AnomalySeverity.Warning,
                        Category = AnomalyCategory.Biometric,
                        StartIndex = i - 1,
                        EndIndex = i,
                        StartTime = points[i - 1].Time,
                        EndTime = points[i].Time,
                        DistanceImpactM = 0,
                        TimeImpactS = 0,
                        Description = $"Heart rate spike: {prevHr}\u2192{hr}bpm ({diff:+0;-0}bpm) between adjacent points",
                    });
                }
            }
        }
    }
}
