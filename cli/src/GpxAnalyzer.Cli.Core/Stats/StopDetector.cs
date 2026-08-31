using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Represents a detected stop period.
/// </summary>
public sealed class Stop
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration { get; init; }
    public double Lat { get; init; } // centroid
    public double Lon { get; init; } // centroid

    /// <summary>
    /// True when the stop's own span contains a recording gap — the device stopped
    /// logging rather than the athlete standing still. Such a stop is an ABSENCE of
    /// fixes, so consumers that reason about what the receiver did between its
    /// samples (GPS drift detection) must not treat it as a stationary period.
    /// </summary>
    public bool SpansRecordingGap { get; init; }
}

/// <summary>
/// Parameters for stop detection.
/// </summary>
public sealed class StopConfig
{
    public double MaxSpeed { get; init; }       // m/s
    public TimeSpan MinDuration { get; init; }
    public double MaxDistance { get; init; }     // meters, 0 = no check
    /// <summary>Tolerate fast spikes shorter than this without breaking the stop.</summary>
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Merge stops separated by a gap shorter than this.</summary>
    public TimeSpan MergeGap { get; init; } = TimeSpan.FromSeconds(90);
}

public static class StopDetector
{
    public const string PresetHiking = "hiking";
    public const string PresetTrail = "trail";
    public const string PresetCycling = "cycling";
    public const string PresetRunning = "running";
    public const string PresetSwimming = "swimming";
    public const string PresetWalking = "walking";

    public static readonly Dictionary<string, StopConfig> Presets = new()
    {
        [PresetHiking] = new() { MaxSpeed = 0.2, MinDuration = TimeSpan.FromMinutes(3), MaxDistance = 30 },
        [PresetTrail] = new() { MaxSpeed = 0.3, MinDuration = TimeSpan.FromMinutes(2), MaxDistance = 50 },
        [PresetCycling] = new() { MaxSpeed = 1.0, MinDuration = TimeSpan.FromSeconds(30), MaxDistance = 100 },
        [PresetRunning] = new() { MaxSpeed = 0.5, MinDuration = TimeSpan.FromMinutes(5), MaxDistance = 150 },
        [PresetSwimming] = new() { MaxSpeed = 0.15, MinDuration = TimeSpan.FromMinutes(2), MaxDistance = 100 },
        [PresetWalking] = new() { MaxSpeed = 0.2, MinDuration = TimeSpan.FromMinutes(3), MaxDistance = 30 },
    };

    public static string DefaultPreset() => PresetHiking;

    /// <summary>
    /// Identifies stop periods in enriched trackpoints (CalcSpeed must be populated).
    /// Uses a grace period to tolerate brief GPS speed spikes within a stop,
    /// and merges nearby stops separated by short gaps.
    /// </summary>
    public static List<Stop> DetectStops(List<TrackPoint> points, StopConfig cfg)
    {
        if (points.Count < 2)
            return [];

        var stops = new List<Stop>();
        bool inStop = false;
        int stopStart = 0;
        int lastSlowIdx = 0; // last index where speed was slow

        for (int i = 1; i < points.Count; i++)
        {
            bool isSlow = points[i].CalcSpeed <= cfg.MaxSpeed;

            if (isSlow && !inStop)
            {
                // Entering a stop
                inStop = true;
                stopStart = i - 1;
                lastSlowIdx = i;
            }
            else if (isSlow && inStop)
            {
                // Still in a stop (or back from grace)
                lastSlowIdx = i;
            }
            else if (!isSlow && inStop)
            {
                // Fast point during a stop — check grace period
                var elapsed = points[i].Time - points[lastSlowIdx].Time;
                if (elapsed > cfg.GracePeriod)
                {
                    // Grace expired: end stop at the last slow point
                    var stop = BuildStop(points, stopStart, lastSlowIdx + 1, cfg);
                    if (stop != null) stops.Add(stop);
                    inStop = false;
                }
                // Otherwise: stay in stop, grace period absorbs the spike
            }
        }

        // Handle stop at end of data
        if (inStop)
        {
            var stop = BuildStop(points, stopStart, lastSlowIdx + 1, cfg);
            if (stop != null) stops.Add(stop);
        }

        // Merge nearby stops
        if (cfg.MergeGap > TimeSpan.Zero && stops.Count > 1)
            stops = MergeStops(stops, points, cfg);

        return stops;
    }

    /// <summary>
    /// Merges stops separated by a gap shorter than mergeGap.
    /// Recomputes centroid from the original points spanning the merged range.
    /// </summary>
    public static List<Stop> MergeStops(List<Stop> stops, List<TrackPoint> points, StopConfig cfg)
    {
        if (stops.Count <= 1)
            return stops;

        var merged = new List<Stop> { stops[0] };

        for (int i = 1; i < stops.Count; i++)
        {
            var prev = merged[^1];
            var gap = stops[i].StartTime - prev.EndTime;

            if (gap <= cfg.MergeGap)
            {
                // Merge: find point indices for centroid recomputation
                var mergedStart = prev.StartTime;
                var mergedEnd = stops[i].EndTime;
                var duration = mergedEnd - mergedStart;

                // Compute centroid from all points in the merged range
                double sumLat = 0, sumLon = 0;
                int count = 0;
                for (int p = 0; p < points.Count; p++)
                {
                    if (points[p].Time >= mergedStart && points[p].Time <= mergedEnd)
                    {
                        sumLat += points[p].Lat;
                        sumLon += points[p].Lon;
                        count++;
                    }
                }

                if (count == 0) count = 1; // safety

                merged[^1] = new Stop
                {
                    StartTime = mergedStart,
                    EndTime = mergedEnd,
                    Duration = duration,
                    Lat = sumLat / count,
                    Lon = sumLon / count,
                    // The merged stop spans a recording gap if either component did,
                    // or if the joint between them is itself one.
                    SpansRecordingGap = prev.SpansRecordingGap || stops[i].SpansRecordingGap
                        || gap > Elevation.ElevationSmoother.GapThreshold,
                };
            }
            else
            {
                merged.Add(stops[i]);
            }
        }

        return merged;
    }

    private static Stop? BuildStop(List<TrackPoint> points, int startIdx, int endIdx, StopConfig cfg)
    {
        int count = endIdx - startIdx;
        if (count < 2)
            return null;

        var duration = points[endIdx - 1].Time - points[startIdx].Time;
        if (duration < cfg.MinDuration)
            return null;

        bool spansGap = HasRecordingGap(points, startIdx, endIdx - 1);

        // Reject if the person actually moved too far — but only when the interval
        // was recorded. Across a recording gap the displacement is movement during
        // unrecorded time, not jitter at a standstill, and the preset limits
        // (30-100 m) are jitter tolerances. Applying them there discards the pause
        // entirely and charges all of it to moving time.
        if (cfg.MaxDistance > 0 && !spansGap)
        {
            double dist = DistanceCalculator.Haversine(
                points[startIdx].Lat, points[startIdx].Lon,
                points[endIdx - 1].Lat, points[endIdx - 1].Lon);
            if (dist > cfg.MaxDistance)
                return null;
        }

        // Compute centroid
        double sumLat = 0, sumLon = 0;
        for (int i = startIdx; i < endIdx; i++)
        {
            sumLat += points[i].Lat;
            sumLon += points[i].Lon;
        }
        double n = count;

        return new Stop
        {
            StartTime = points[startIdx].Time,
            EndTime = points[endIdx - 1].Time,
            Duration = duration,
            Lat = sumLat / n,
            Lon = sumLon / n,
            SpansRecordingGap = spansGap
        };
    }

    /// <summary>
    /// True when any interval inside [startIdx, endIdx] exceeds the pipeline's
    /// recording-gap threshold — i.e. the device stopped logging.
    /// </summary>
    private static bool HasRecordingGap(List<TrackPoint> points, int startIdx, int endIdx)
    {
        for (int i = startIdx + 1; i <= endIdx && i < points.Count; i++)
            if (points[i].Time - points[i - 1].Time > Elevation.ElevationSmoother.GapThreshold)
                return true;
        return false;
    }

    public static TimeSpan TotalStopTime(List<Stop> stops)
    {
        var total = TimeSpan.Zero;
        foreach (var s in stops)
            total += s.Duration;
        return total;
    }

    public static Stop? LongestStop(List<Stop> stops)
    {
        if (stops.Count == 0) return null;
        var longest = stops[0];
        for (int i = 1; i < stops.Count; i++)
        {
            if (stops[i].Duration > longest.Duration)
                longest = stops[i];
        }
        return longest;
    }

    public static TimeSpan AvgStopDuration(List<Stop> stops)
    {
        if (stops.Count == 0) return TimeSpan.Zero;
        return TotalStopTime(stops) / stops.Count;
    }
}
