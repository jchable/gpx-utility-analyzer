using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Anomaly.Detectors;

/// <summary>
/// Detects data quality issues: low point density and constant elevation (barometer failure).
/// </summary>
public static class DataQualityDetector
{
    public static List<TrackAnomaly> Detect(List<TrackPoint> points, double totalDistanceM,
        bool hasDemCorrection, AnomalyConfig cfg)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2 || totalDistanceM < 100)
            return anomalies;

        DetectLowPointDensity(points, totalDistanceM, cfg, anomalies);
        DetectConstantElevation(points, hasDemCorrection, cfg, anomalies);

        return anomalies;
    }

    private static void DetectLowPointDensity(List<TrackPoint> points, double totalDistanceM,
        AnomalyConfig cfg, List<TrackAnomaly> anomalies)
    {
        double distKm = totalDistanceM / 1000;
        if (distKm < 0.1) return;

        double pointsPerKm = points.Count / distKm;
        if (pointsPerKm < cfg.MinPointsPerKm)
        {
            anomalies.Add(new TrackAnomaly
            {
                Type = AnomalyType.LowPointDensity,
                Severity = AnomalySeverity.Warning,
                Category = AnomalyCategory.DataQuality,
                StartIndex = 0,
                EndIndex = points.Count - 1,
                StartTime = points[0].Time,
                EndTime = points[^1].Time,
                DistanceImpactM = 0,
                TimeImpactS = 0,
                Description = $"Low point density: {pointsPerKm:F1} points/km (minimum recommended: {cfg.MinPointsPerKm:F0})",
            });
        }
    }

    private static void DetectConstantElevation(List<TrackPoint> points, bool hasDemCorrection,
        AnomalyConfig cfg, List<TrackAnomaly> anomalies)
    {
        if (points.Count < cfg.ConstantElevationMinPoints)
            return;

        double minEle = double.MaxValue, maxEle = double.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Ele < minEle) minEle = points[i].Ele;
            if (points[i].Ele > maxEle) maxEle = points[i].Ele;
        }

        double range = maxEle - minEle;
        if (range >= cfg.ConstantElevationRangeM)
            return;

        var severity = hasDemCorrection ? AnomalySeverity.Warning : AnomalySeverity.Critical;

        anomalies.Add(new TrackAnomaly
        {
            Type = AnomalyType.ConstantElevation,
            Severity = severity,
            Category = AnomalyCategory.DataQuality,
            StartIndex = 0,
            EndIndex = points.Count - 1,
            StartTime = points[0].Time,
            EndTime = points[^1].Time,
            DistanceImpactM = 0,
            TimeImpactS = 0,
            Description = $"Constant elevation: {range:F1}m range across {points.Count} points — possible barometer failure",
        });
    }
}
