using GpxAnalyzer.Cli.Core.Anomaly.Detectors;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Anomaly;

/// <summary>
/// Orchestrates all anomaly detectors and produces a consolidated report.
/// </summary>
public static class AnomalyDetector
{
    /// <summary>
    /// Detect all anomalies in the enriched track points.
    /// Must be called AFTER SpeedCalculator.EnrichPoints() and StopDetector.DetectStops()
    /// so that CalcSpeed, DistFromPrev, and stop data are available.
    /// </summary>
    public static AnomalyReport Detect(
        List<TrackPoint> points,
        List<Stop> stops,
        double maxReasonableSpeed,
        double totalDistanceM,
        int clampedCount,
        bool hasDemCorrection,
        List<double>? rawElevations,
        AnomalyConfig cfg)
    {
        if (points.Count < 2)
            return new AnomalyReport { QualityScore = 100 };

        var anomalies = new List<TrackAnomaly>();

        // Run all category detectors
        anomalies.AddRange(PositionAnomalyDetector.Detect(points, stops, cfg));
        anomalies.AddRange(SpeedAnomalyDetector.Detect(points, maxReasonableSpeed, clampedCount, cfg));
        anomalies.AddRange(ElevationAnomalyDetector.Detect(points, cfg));
        anomalies.AddRange(TemporalAnomalyDetector.Detect(points));
        anomalies.AddRange(BiometricAnomalyDetector.Detect(points, cfg));
        anomalies.AddRange(DataQualityDetector.Detect(points, totalDistanceM, hasDemCorrection, cfg));

        // Raw elevation anomalies (pre-smoothing data). Skipped when DEM
        // correction ran: the spike no longer exists in the processed
        // elevations, so "correcting" it would overwrite accurate SRTM data.
        if (rawElevations != null && !hasDemCorrection)
            anomalies.AddRange(ElevationAnomalyDetector.DetectRaw(points, rawElevations, cfg));

        // Deduplicate: remove SpeedBiometricMismatch that overlaps with GpsFrozen
        // (GpsFrozen already uses biometric indicators as confirmation)
        var frozenRanges = anomalies
            .Where(a => a.Type == AnomalyType.GpsFrozen)
            .ToList();

        if (frozenRanges.Count > 0)
        {
            anomalies.RemoveAll(a =>
                a.Type == AnomalyType.SpeedBiometricMismatch &&
                frozenRanges.Any(f => a.StartIndex >= f.StartIndex && a.EndIndex <= f.EndIndex));
        }

        // Sort by start index for consistent output
        anomalies.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));

        double totalDistImpact = anomalies.Sum(a => a.DistanceImpactM);
        double totalTimeImpact = anomalies.Sum(a => a.TimeImpactS);

        return new AnomalyReport
        {
            Anomalies = anomalies,
            QualityScore = AnomalyReport.ComputeQualityScore(anomalies),
            TotalDistanceImpactM = totalDistImpact,
            TotalTimeImpactS = totalTimeImpact,
        };
    }
}
