using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Anomaly.Detectors;

/// <summary>
/// Detects temporal anomalies: backward timestamps and duplicate timestamps.
/// </summary>
public static class TemporalAnomalyDetector
{
    public static List<TrackAnomaly> Detect(List<TrackPoint> points)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2)
            return anomalies;

        DetectBackwardTime(points, anomalies);
        DetectDuplicateTimestamps(points, anomalies);

        return anomalies;
    }

    private static void DetectBackwardTime(List<TrackPoint> points, List<TrackAnomaly> anomalies)
    {
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].Time < points[i - 1].Time)
            {
                double diff = (points[i - 1].Time - points[i].Time).TotalSeconds;
                anomalies.Add(new TrackAnomaly
                {
                    Type = AnomalyType.BackwardTime,
                    Severity = AnomalySeverity.Critical,
                    Category = AnomalyCategory.Temporal,
                    StartIndex = i - 1,
                    EndIndex = i,
                    StartTime = points[i - 1].Time,
                    EndTime = points[i].Time,
                    DistanceImpactM = 0,
                    TimeImpactS = -diff,
                    Description = $"Backward timestamp: point {i} is {diff:F0}s earlier than previous point",
                });
            }
        }
    }

    private static void DetectDuplicateTimestamps(List<TrackPoint> points, List<TrackAnomaly> anomalies)
    {
        int i = 1;
        while (i < points.Count)
        {
            if (points[i].Time == points[i - 1].Time)
            {
                int runStart = i - 1;
                while (i < points.Count && points[i].Time == points[runStart].Time)
                    i++;

                int count = i - runStart;
                anomalies.Add(new TrackAnomaly
                {
                    Type = AnomalyType.DuplicateTimestamp,
                    Severity = AnomalySeverity.Info,
                    Category = AnomalyCategory.Temporal,
                    StartIndex = runStart,
                    EndIndex = i - 1,
                    StartTime = points[runStart].Time,
                    EndTime = points[runStart].Time,
                    DistanceImpactM = 0,
                    TimeImpactS = 0,
                    Description = $"Duplicate timestamp: {count} points share the same timestamp",
                });
                continue;
            }
            i++;
        }
    }
}
