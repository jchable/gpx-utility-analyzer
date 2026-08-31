using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Anomaly;

/// <summary>
/// Applies corrections to trackpoints for correctable anomalies.
/// Only runs when --fix-anomalies is explicitly requested.
/// </summary>
public static class AnomalyCorrector
{
    /// <summary>
    /// Applies corrections and returns an updated report with WasCorrected flags.
    /// Mutates points in-place (consistent with pipeline pattern).
    /// </summary>
    public static AnomalyReport ApplyCorrections(List<TrackPoint> points, AnomalyReport report)
    {
        var corrected = new List<TrackAnomaly>(report.Anomalies.Count);

        foreach (var anomaly in report.Anomalies)
        {
            var result = anomaly.Type switch
            {
                AnomalyType.GpsFrozen => CorrectGpsFrozen(points, anomaly),
                AnomalyType.GpsDrift => CorrectGpsDrift(points, anomaly),
                AnomalyType.BackwardTime => CorrectBackwardTime(points, anomaly),
                AnomalyType.DuplicateTimestamp => CorrectDuplicateTimestamp(points, anomaly),
                AnomalyType.ElevationSpike => CorrectElevationSpike(points, anomaly),
                AnomalyType.HeartRateOutOfRange => CorrectHrOutOfRange(points, anomaly),
                _ => anomaly // Not correctable
            };
            corrected.Add(result);
        }

        return new AnomalyReport
        {
            Anomalies = corrected,
            QualityScore = report.QualityScore,
            TotalDistanceImpactM = report.TotalDistanceImpactM,
            TotalTimeImpactS = report.TotalTimeImpactS,
            CorrectionApplied = true,
        };
    }

    /// <summary>
    /// Overrides DistFromPrev inside corrected GPS-frozen sections. Linear lat/lon
    /// interpolation cannot recover loop-course distance, so the distance is
    /// estimated from the post-correction average moving speed.
    /// </summary>
    public static void ApplyFrozenSectionDistances(List<TrackPoint> points, Summary s)
    {
        if (s.AnomalyReport is null) return;

        var frozenRanges = new List<(int Start, int End, double Duration)>();
        foreach (var a in s.AnomalyReport.Anomalies)
        {
            if (a.Type == AnomalyType.GpsFrozen && a.WasCorrected && a.TimeImpactS > 0)
                frozenRanges.Add((a.StartIndex, a.EndIndex, a.TimeImpactS));
        }
        if (frozenRanges.Count == 0) return;

        // Build a set of frozen point indices for fast lookup
        var frozenIndices = new HashSet<int>();
        foreach (var (start, end, _) in frozenRanges)
            for (int i = start; i <= end; i++)
                frozenIndices.Add(i);

        // Sum post-enrichment distance for non-frozen points only
        // (drift sections are already collapsed to centroid by ApplyCorrections,
        //  so EnrichPoints produces ~0 DistFromPrev for them)
        double healthyDist = 0;
        for (int i = 1; i < points.Count; i++)
            if (!frozenIndices.Contains(i))
                healthyDist += points[i].DistFromPrev;

        double totalFrozenDuration = frozenRanges.Sum(r => r.Duration);
        double nonFrozenMovingS = s.MovingTime.TotalSeconds - totalFrozenDuration;

        // MovingTime may or may not already exclude the frozen sections (a frozen
        // section usually reads as a stop). Prefer the variant that excludes them;
        // fall back to plain MovingTime when the subtraction is not meaningful,
        // instead of collapsing the estimate to zero.
        double avgMovingSpeed =
            nonFrozenMovingS > 0 ? healthyDist / nonFrozenMovingS
          : s.MovingTime.TotalSeconds > 0 ? healthyDist / s.MovingTime.TotalSeconds
          : 0;

        foreach (var (start, end, duration) in frozenRanges)
        {
            double estimatedDist = avgMovingSpeed * duration;
            int count = end - start + 1;
            double distPerPoint = estimatedDist / count;
            for (int i = start; i <= end && i < points.Count; i++)
                points[i].DistFromPrev = distPerPoint;
        }
    }

    /// <summary>
    /// Recalculates distance and speed stats after corrections were applied.
    /// Does NOT re-run the full pipeline — only updates affected summary fields.
    /// </summary>
    public static void RecalculateStats(List<TrackPoint> points, Summary s)
    {
        // Re-enrich speeds and distances
        SpeedCalculator.EnrichPoints(points);

        ApplyFrozenSectionDistances(points, s);

        // Re-sum distances. 3D is derived from the same segments as 2D — see
        // ComputePipeline step 6-7; the two expressions must stay in sync.
        s.TotalDistance = 0;
        s.TotalDistance3D = 0;
        for (int i = 1; i < points.Count; i++)
        {
            double horizontal = points[i].DistFromPrev;
            s.TotalDistance += horizontal;

            if (horizontal <= 0) continue;

            double dEle = points[i].Ele - points[i - 1].Ele;
            s.TotalDistance3D += Math.Sqrt(horizontal * horizontal + dEle * dEle);
        }

        // Re-compute speed
        s.Speed = SpeedCalculator.ComputeSpeed(s.TotalDistance, s.TotalTime, s.MovingTime);
        s.Speed.MaxSpeed = SpeedCalculator.MaxSpeedFromPoints(points);

        // Re-compute points per km
        if (s.TotalDistance > 0)
            s.PointsPerKm = points.Count / (s.TotalDistance / 1000);
    }

    /// <summary>
    /// Interpolates lat/lon linearly through frozen section.
    /// Estimates distance from surrounding segments' speed.
    /// </summary>
    private static TrackAnomaly CorrectGpsFrozen(List<TrackPoint> points, TrackAnomaly anomaly)
    {
        int start = anomaly.StartIndex;
        int end = anomaly.EndIndex;

        // Find the last good point before and first good point after.
        // TrackPoint is a mutable class, so the fallback aliased the very object
        // the loop overwrites on its first iteration when start == 0. Snapshot
        // the coordinates before interpolating.
        var anchor = start > 0 ? points[start - 1] : points[start];
        double lat0 = anchor.Lat, lon0 = anchor.Lon;

        var tail = end < points.Count - 1 ? points[end + 1] : points[end];
        double lat1 = tail.Lat, lon1 = tail.Lon;

        int count = end - start + 1;

        // Linearly interpolate positions through the frozen section
        for (int i = start; i <= end; i++)
        {
            double t = (double)(i - start + 1) / (count + 1);
            points[i].Lat = lat0 + (lat1 - lat0) * t;
            points[i].Lon = lon0 + (lon1 - lon0) * t;
        }

        return new TrackAnomaly
        {
            Type = anomaly.Type,
            Severity = anomaly.Severity,
            Category = anomaly.Category,
            StartIndex = anomaly.StartIndex,
            EndIndex = anomaly.EndIndex,
            StartTime = anomaly.StartTime,
            EndTime = anomaly.EndTime,
            DistanceImpactM = anomaly.DistanceImpactM,
            TimeImpactS = anomaly.TimeImpactS,
            Description = anomaly.Description,
            WasCorrected = true,
        };
    }

    /// <summary>
    /// Collapses drifting points during stops to the centroid position.
    /// </summary>
    private static TrackAnomaly CorrectGpsDrift(List<TrackPoint> points, TrackAnomaly anomaly)
    {
        // Compute centroid of all points in the drift zone
        double sumLat = 0, sumLon = 0;
        int count = 0;
        for (int i = anomaly.StartIndex; i <= anomaly.EndIndex; i++)
        {
            sumLat += points[i].Lat;
            sumLon += points[i].Lon;
            count++;
        }

        double centLat = sumLat / count;
        double centLon = sumLon / count;

        // Collapse all points to centroid
        for (int i = anomaly.StartIndex; i <= anomaly.EndIndex; i++)
        {
            points[i].Lat = centLat;
            points[i].Lon = centLon;
            points[i].DistFromPrev = 0;
        }

        return MarkCorrected(anomaly);
    }

    /// <summary>
    /// Sets backward timestamp to previous + 1 second.
    /// </summary>
    private static TrackAnomaly CorrectBackwardTime(List<TrackPoint> points, TrackAnomaly anomaly)
    {
        int idx = anomaly.EndIndex;
        if (idx > 0 && idx < points.Count)
        {
            points[idx].Time = points[idx - 1].Time.AddSeconds(1);
        }

        return MarkCorrected(anomaly);
    }

    /// <summary>
    /// Interpolates duplicate timestamps evenly between surrounding unique timestamps.
    /// </summary>
    private static TrackAnomaly CorrectDuplicateTimestamp(List<TrackPoint> points, TrackAnomaly anomaly)
    {
        int start = anomaly.StartIndex;
        int end = anomaly.EndIndex;

        // Find surrounding unique timestamps
        var timeBefore = points[start].Time;
        var timeAfter = end < points.Count - 1
            ? points[end + 1].Time
            : timeBefore.AddSeconds(end - start + 1);

        double totalSeconds = (timeAfter - timeBefore).TotalSeconds;
        int count = end - start + 1;

        for (int i = start; i <= end; i++)
        {
            double fraction = (double)(i - start) / count;
            points[i].Time = timeBefore.AddSeconds(totalSeconds * fraction);
        }

        return MarkCorrected(anomaly);
    }

    /// <summary>
    /// Interpolates elevation linearly between surrounding healthy points.
    /// </summary>
    private static TrackAnomaly CorrectElevationSpike(List<TrackPoint> points, TrackAnomaly anomaly)
    {
        int start = anomaly.StartIndex;
        int end = anomaly.EndIndex;

        double eleBefore = start > 0 ? points[start - 1].Ele : points[start].Ele;
        double eleAfter = end < points.Count - 1 ? points[end + 1].Ele : points[end].Ele;

        int count = end - start + 1;
        for (int i = start; i <= end; i++)
        {
            double t = (double)(i - start + 1) / (count + 1);
            points[i].Ele = eleBefore + (eleAfter - eleBefore) * t;
        }

        return MarkCorrected(anomaly);
    }

    /// <summary>
    /// Sets out-of-range heart rate values to null (excluded from biometric stats).
    /// </summary>
    private static TrackAnomaly CorrectHrOutOfRange(List<TrackPoint> points, TrackAnomaly anomaly)
    {
        for (int i = anomaly.StartIndex; i <= anomaly.EndIndex; i++)
        {
            points[i].HeartRate = null;
        }

        return MarkCorrected(anomaly);
    }

    private static TrackAnomaly MarkCorrected(TrackAnomaly a) => new()
    {
        Type = a.Type,
        Severity = a.Severity,
        Category = a.Category,
        StartIndex = a.StartIndex,
        EndIndex = a.EndIndex,
        StartTime = a.StartTime,
        EndTime = a.EndTime,
        DistanceImpactM = a.DistanceImpactM,
        TimeImpactS = a.TimeImpactS,
        Description = a.Description,
        WasCorrected = true,
    };
}
