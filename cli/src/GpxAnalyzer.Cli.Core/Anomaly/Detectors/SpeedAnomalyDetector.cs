using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Anomaly.Detectors;

/// <summary>
/// Detects speed anomalies: speed spikes and speed/biometric mismatches.
/// </summary>
public static class SpeedAnomalyDetector
{
    public static List<TrackAnomaly> Detect(List<TrackPoint> points, double maxReasonableSpeed,
        int clampedCount, AnomalyConfig cfg)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2)
            return anomalies;

        DetectSpeedSpikes(points, maxReasonableSpeed, clampedCount, anomalies);
        DetectSpeedBiometricMismatch(points, cfg, anomalies);

        return anomalies;
    }

    /// <summary>
    /// Reports speed spikes (points where CalcSpeed was clamped to 0).
    /// These are already corrected by ClampSpeeds; we just report them.
    /// </summary>
    private static void DetectSpeedSpikes(List<TrackPoint> points, double maxReasonableSpeed,
        int clampedCount, List<TrackAnomaly> anomalies)
    {
        if (clampedCount == 0 || maxReasonableSpeed <= 0)
            return;

        // Find clusters of clamped points (CalcSpeed == 0 && DistFromPrev == 0 but time gap is small)
        int i = 1;
        while (i < points.Count)
        {
            // A clamped point has CalcSpeed=0 and DistFromPrev=0 but a normal time gap
            if (points[i].CalcSpeed == 0 && points[i].DistFromPrev == 0)
            {
                double dt = (points[i].Time - points[i - 1].Time).TotalSeconds;
                if (dt > 0 && dt < 10) // Normal time gap, but speed was clamped
                {
                    int clusterStart = i;
                    while (i < points.Count && points[i].CalcSpeed == 0 && points[i].DistFromPrev == 0)
                    {
                        double nextDt = i + 1 < points.Count
                            ? (points[i + 1].Time - points[i].Time).TotalSeconds : 999;
                        if (nextDt > 10) break;
                        i++;
                    }

                    anomalies.Add(new TrackAnomaly
                    {
                        Type = AnomalyType.SpeedSpike,
                        Severity = AnomalySeverity.Warning,
                        Category = AnomalyCategory.Speed,
                        StartIndex = clusterStart,
                        EndIndex = i - 1,
                        StartTime = points[clusterStart].Time,
                        EndTime = points[i - 1].Time,
                        DistanceImpactM = 0,
                        TimeImpactS = 0,
                        Description = $"Speed spike: {i - clusterStart} points exceeded {maxReasonableSpeed * 3.6:F1} km/h threshold",
                    });
                    continue;
                }
            }
            i++;
        }
    }

    /// <summary>
    /// Detects inconsistencies between speed and biometric data.
    /// High cadence with zero movement confirms GPS frozen; high speed with zero cadence is suspect.
    /// </summary>
    private static void DetectSpeedBiometricMismatch(List<TrackPoint> points, AnomalyConfig cfg,
        List<TrackAnomaly> anomalies)
    {
        int i = 1;
        while (i < points.Count)
        {
            // High cadence + no movement = GPS frozen confirmation
            if (points[i].CalcSpeed < 0.1 &&
                points[i].Cadence is int cad && cad > cfg.ActiveCadenceThreshold)
            {
                int runStart = i;
                while (i < points.Count &&
                       points[i].CalcSpeed < 0.1 &&
                       points[i].Cadence is int c && c > cfg.ActiveCadenceThreshold)
                {
                    i++;
                }

                int runLength = i - runStart;
                if (runLength >= 3) // Only report if sustained
                {
                    anomalies.Add(new TrackAnomaly
                    {
                        Type = AnomalyType.SpeedBiometricMismatch,
                        Severity = AnomalySeverity.Warning,
                        Category = AnomalyCategory.Speed,
                        StartIndex = runStart,
                        EndIndex = i - 1,
                        StartTime = points[runStart].Time,
                        EndTime = points[i - 1].Time,
                        DistanceImpactM = 0,
                        TimeImpactS = 0,
                        Description = $"Active cadence ({cad}rpm) with zero movement for {runLength} points — confirms GPS position issue",
                    });
                }
                continue;
            }
            i++;
        }
    }
}
