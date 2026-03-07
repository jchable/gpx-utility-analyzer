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
    /// Recalculates distance and speed stats after corrections were applied.
    /// Does NOT re-run the full pipeline — only updates affected summary fields.
    /// </summary>
    public static void RecalculateStats(List<TrackPoint> points, Summary s)
    {
        // Re-enrich speeds and distances
        SpeedCalculator.EnrichPoints(points);

        // Re-sum distances
        s.TotalDistance = 0;
        s.TotalDistance3D = 0;
        for (int i = 1; i < points.Count; i++)
        {
            s.TotalDistance += points[i].DistFromPrev;
            s.TotalDistance3D += DistanceCalculator.Distance3D(
                points[i - 1].Lat, points[i - 1].Lon, points[i - 1].Ele,
                points[i].Lat, points[i].Lon, points[i].Ele);
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

        // Find the last good point before and first good point after
        var p0 = start > 0 ? points[start - 1] : points[start];
        var p1 = end < points.Count - 1 ? points[end + 1] : points[end];

        int count = end - start + 1;

        // Linearly interpolate positions through the frozen section
        for (int i = start; i <= end; i++)
        {
            double t = (double)(i - start + 1) / (count + 1);
            points[i].Lat = p0.Lat + (p1.Lat - p0.Lat) * t;
            points[i].Lon = p0.Lon + (p1.Lon - p0.Lon) * t;
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
