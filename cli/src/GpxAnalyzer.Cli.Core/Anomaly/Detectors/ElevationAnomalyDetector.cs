using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Anomaly.Detectors;

/// <summary>
/// Detects elevation anomalies: spikes and impossible grades.
/// </summary>
public static class ElevationAnomalyDetector
{
    /// <summary>
    /// Detects elevation anomalies from processed (post-smoothing) points.
    /// </summary>
    public static List<TrackAnomaly> Detect(List<TrackPoint> points, AnomalyConfig cfg)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2)
            return anomalies;

        DetectImpossibleGrade(points, cfg, anomalies);

        return anomalies;
    }

    /// <summary>
    /// Detects elevation spikes from raw (pre-smoothing) elevations.
    /// Called with the captured raw elevation values before DEM/smoothing was applied.
    /// </summary>
    public static List<TrackAnomaly> DetectRaw(List<TrackPoint> points, List<double> rawElevations,
        AnomalyConfig cfg)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2 || rawElevations.Count != points.Count)
            return anomalies;

        for (int i = 1; i < points.Count; i++)
        {
            double dEle = Math.Abs(rawElevations[i] - rawElevations[i - 1]);
            double dist = points[i].DistFromPrev;

            // Only flag spikes where elevation change is huge relative to distance
            if (dEle > cfg.ElevationSpikeThresholdM && dist < 50)
            {
                anomalies.Add(new TrackAnomaly
                {
                    Type = AnomalyType.ElevationSpike,
                    Severity = AnomalySeverity.Warning,
                    Category = AnomalyCategory.Elevation,
                    StartIndex = i - 1,
                    EndIndex = i,
                    StartTime = points[i - 1].Time,
                    EndTime = points[i].Time,
                    DistanceImpactM = 0,
                    TimeImpactS = 0,
                    Description = $"Elevation spike: {dEle:F0}m change over {dist:F0}m distance (raw data, before smoothing)",
                });
            }
        }

        return anomalies;
    }

    /// <summary>
    /// Detects segments with physically impossible grades.
    /// </summary>
    private static void DetectImpossibleGrade(List<TrackPoint> points, AnomalyConfig cfg,
        List<TrackAnomaly> anomalies)
    {
        for (int i = 1; i < points.Count; i++)
        {
            double dist = points[i].DistFromPrev;
            if (dist < 2) continue; // Skip very short segments

            double dEle = Math.Abs(points[i].Ele - points[i - 1].Ele);
            double gradePercent = (dEle / dist) * 100;

            if (gradePercent > cfg.ImpossibleGradePercent)
            {
                anomalies.Add(new TrackAnomaly
                {
                    Type = AnomalyType.ImpossibleGrade,
                    Severity = AnomalySeverity.Warning,
                    Category = AnomalyCategory.Elevation,
                    StartIndex = i - 1,
                    EndIndex = i,
                    StartTime = points[i - 1].Time,
                    EndTime = points[i].Time,
                    DistanceImpactM = 0,
                    TimeImpactS = 0,
                    Description = $"Impossible grade: {gradePercent:F0}% over {dist:F0}m (elevation change: {dEle:F0}m)",
                });
            }
        }
    }
}
