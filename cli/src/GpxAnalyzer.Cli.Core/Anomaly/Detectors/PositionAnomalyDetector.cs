using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Anomaly.Detectors;

/// <summary>
/// Detects GPS position anomalies: frozen GPS, teleportation, drift, signal loss.
/// </summary>
public static class PositionAnomalyDetector
{
    public static List<TrackAnomaly> Detect(List<TrackPoint> points, List<Stop> stops, AnomalyConfig cfg)
    {
        var anomalies = new List<TrackAnomaly>();

        if (points.Count < 2)
            return anomalies;

        DetectGpsFrozen(points, cfg, anomalies);
        DetectSignalLoss(points, cfg, anomalies);
        DetectGpsDrift(points, stops, cfg, anomalies);

        return anomalies;
    }

    /// <summary>
    /// Detects sections where GPS coordinates are frozen (identical) while biometrics indicate movement.
    /// </summary>
    private static void DetectGpsFrozen(List<TrackPoint> points, AnomalyConfig cfg, List<TrackAnomaly> anomalies)
    {
        double eps = cfg.GpsFrozenEpsilon;
        int i = 0;

        while (i < points.Count)
        {
            int runStart = i;
            double anchorLat = points[i].Lat;
            double anchorLon = points[i].Lon;
            i++;

            // Find consecutive identical-position points
            while (i < points.Count &&
                   Math.Abs(points[i].Lat - anchorLat) < eps &&
                   Math.Abs(points[i].Lon - anchorLon) < eps)
            {
                i++;
            }

            int runLength = i - runStart;
            if (runLength < cfg.GpsFrozenMinPoints)
                continue;

            // Check if biometrics indicate movement during this frozen period
            bool hasMovementIndicator = false;
            int firstHr = -1, lastHr = -1;
            int maxCadence = 0;

            for (int j = runStart; j < runStart + runLength; j++)
            {
                if (points[j].Cadence is int cad && cad > cfg.ActiveCadenceThreshold)
                    maxCadence = Math.Max(maxCadence, cad);

                if (points[j].HeartRate is int hr)
                {
                    if (firstHr < 0) firstHr = hr;
                    lastHr = hr;
                }
            }

            // Movement confirmed if cadence is active OR HR increased significantly
            hasMovementIndicator = maxCadence > cfg.ActiveCadenceThreshold ||
                                   (firstHr >= 0 && lastHr >= 0 && lastHr - firstHr > 20);

            if (!hasMovementIndicator)
                continue;

            // Estimate distance impact using median speed of surrounding non-frozen segments
            double duration = (points[runStart + runLength - 1].Time - points[runStart].Time).TotalSeconds;
            double estimatedSpeed = EstimateNeighboringSpeed(points, runStart, runStart + runLength - 1);
            double distanceLost = estimatedSpeed * duration;

            string desc = $"GPS position frozen for {runLength} points ({FormatDuration(duration)}) " +
                           $"while biometrics indicate movement";
            if (maxCadence > 0)
                desc += $" (cadence={maxCadence}rpm";
            if (firstHr >= 0 && lastHr >= 0)
                desc += maxCadence > 0
                    ? $", HR {firstHr}\u2192{lastHr}bpm)"
                    : $" (HR {firstHr}\u2192{lastHr}bpm)";
            else if (maxCadence > 0)
                desc += ")";

            anomalies.Add(new TrackAnomaly
            {
                Type = AnomalyType.GpsFrozen,
                Severity = AnomalySeverity.Critical,
                Category = AnomalyCategory.Position,
                StartIndex = runStart,
                EndIndex = runStart + runLength - 1,
                StartTime = points[runStart].Time,
                EndTime = points[runStart + runLength - 1].Time,
                DistanceImpactM = -distanceLost,
                TimeImpactS = duration,
                Description = desc,
            });
        }
    }

    /// <summary>
    /// Detects large time gaps between consecutive points (GPS signal loss).
    /// </summary>
    private static void DetectSignalLoss(List<TrackPoint> points, AnomalyConfig cfg, List<TrackAnomaly> anomalies)
    {
        for (int i = 1; i < points.Count; i++)
        {
            double dt = (points[i].Time - points[i - 1].Time).TotalSeconds;
            if (dt <= cfg.SignalLossThresholdS)
                continue;

            double straightLineDist = DistanceCalculator.Haversine(
                points[i - 1].Lat, points[i - 1].Lon,
                points[i].Lat, points[i].Lon);
            double estimatedSpeed = EstimateNeighboringSpeed(points, i - 1, i);
            double estimatedDist = estimatedSpeed * dt;
            double distanceLost = estimatedDist - straightLineDist;

            if (distanceLost < 1) distanceLost = 0;

            anomalies.Add(new TrackAnomaly
            {
                Type = AnomalyType.SignalLoss,
                Severity = AnomalySeverity.Warning,
                Category = AnomalyCategory.Position,
                StartIndex = i - 1,
                EndIndex = i,
                StartTime = points[i - 1].Time,
                EndTime = points[i].Time,
                DistanceImpactM = -distanceLost,
                TimeImpactS = dt,
                Description = $"Signal loss: {FormatDuration(dt)} gap between consecutive points",
            });
        }
    }

    /// <summary>
    /// Detects GPS drift (position oscillation) during detected stops.
    /// </summary>
    private static void DetectGpsDrift(List<TrackPoint> points, List<Stop> stops, AnomalyConfig cfg,
        List<TrackAnomaly> anomalies)
    {
        foreach (var stop in stops)
        {
            // A recording gap is not drift. Drift is the receiver reporting movement
            // while stationary; a gap is the absence of fixes entirely, and its two
            // endpoints are far apart because hours passed, not because the receiver
            // misbehaved. The stop detector already classified it.
            if (stop.SpansRecordingGap)
                continue;

            // Find point indices for this stop
            int startIdx = -1, endIdx = -1;
            for (int i = 0; i < points.Count; i++)
            {
                if (startIdx < 0 && points[i].Time >= stop.StartTime)
                    startIdx = i;
                if (points[i].Time <= stop.EndTime)
                    endIdx = i;
                if (points[i].Time > stop.EndTime)
                    break;
            }

            if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                continue;

            // Compute max drift from centroid
            double maxDrift = 0;
            double inflatedDistance = 0;

            for (int i = startIdx; i <= endIdx; i++)
            {
                double drift = DistanceCalculator.Haversine(
                    stop.Lat, stop.Lon, points[i].Lat, points[i].Lon);
                maxDrift = Math.Max(maxDrift, drift);
                inflatedDistance += points[i].DistFromPrev;
            }

            if (maxDrift < cfg.GpsDriftThresholdM)
                continue;

            var severity = inflatedDistance > 100 ? AnomalySeverity.Warning : AnomalySeverity.Info;

            anomalies.Add(new TrackAnomaly
            {
                Type = AnomalyType.GpsDrift,
                Severity = severity,
                Category = AnomalyCategory.Position,
                StartIndex = startIdx,
                EndIndex = endIdx,
                StartTime = stop.StartTime,
                EndTime = stop.EndTime,
                DistanceImpactM = inflatedDistance,
                TimeImpactS = 0,
                Description = $"Position oscillation during stop: max drift {maxDrift:F0}m from centroid, inflated distance: {inflatedDistance:F0}m",
            });
        }
    }

    /// <summary>
    /// Estimates speed from neighboring non-anomalous segments (median of speeds around the anomaly zone).
    /// </summary>
    private static double EstimateNeighboringSpeed(List<TrackPoint> points, int startIdx, int endIdx)
    {
        const int sampleSize = 50;
        var speeds = new List<double>();

        // Sample before
        for (int i = Math.Max(1, startIdx - sampleSize); i < startIdx; i++)
        {
            if (points[i].CalcSpeed > 0.5)
                speeds.Add(points[i].CalcSpeed);
        }

        // Sample after
        for (int i = endIdx + 1; i < Math.Min(points.Count, endIdx + sampleSize + 1); i++)
        {
            if (points[i].CalcSpeed > 0.5)
                speeds.Add(points[i].CalcSpeed);
        }

        if (speeds.Count == 0)
            return 0;

        // Return median
        speeds.Sort();
        return speeds[speeds.Count / 2];
    }

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m{ts.Seconds:D2}s";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m{ts.Seconds:D2}s";
        return $"{ts.Seconds}s";
    }
}
