using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Stats;

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
}

/// <summary>
/// Parameters for stop detection.
/// </summary>
public sealed class StopConfig
{
    public double MaxSpeed { get; init; }       // m/s
    public TimeSpan MinDuration { get; init; }
    public double MaxDistance { get; init; }     // meters, 0 = no check
}

public static class StopDetector
{
    public const string PresetHiking = "hiking";
    public const string PresetTrail = "trail";
    public const string PresetCycling = "cycling";

    public static readonly Dictionary<string, StopConfig> Presets = new()
    {
        [PresetHiking] = new() { MaxSpeed = 0.2, MinDuration = TimeSpan.FromMinutes(3), MaxDistance = 30 },
        [PresetTrail] = new() { MaxSpeed = 0.3, MinDuration = TimeSpan.FromMinutes(2), MaxDistance = 50 },
        [PresetCycling] = new() { MaxSpeed = 1.0, MinDuration = TimeSpan.FromSeconds(30), MaxDistance = 100 },
    };

    public static string DefaultPreset() => PresetHiking;

    /// <summary>
    /// Identifies stop periods in enriched trackpoints (CalcSpeed must be populated).
    /// </summary>
    public static List<Stop> DetectStops(List<TrackPoint> points, StopConfig cfg)
    {
        if (points.Count < 2)
            return [];

        var stops = new List<Stop>();
        bool inStop = false;
        int stopStart = 0;

        for (int i = 1; i < points.Count; i++)
        {
            bool isSlow = points[i].CalcSpeed <= cfg.MaxSpeed;

            if (isSlow && !inStop)
            {
                inStop = true;
                stopStart = i - 1;
            }
            else if (!isSlow && inStop)
            {
                var stop = BuildStop(points, stopStart, i, cfg);
                if (stop != null) stops.Add(stop);
                inStop = false;
            }
        }

        // Handle stop at end of data
        if (inStop)
        {
            var stop = BuildStop(points, stopStart, points.Count, cfg);
            if (stop != null) stops.Add(stop);
        }

        return stops;
    }

    private static Stop? BuildStop(List<TrackPoint> points, int startIdx, int endIdx, StopConfig cfg)
    {
        int count = endIdx - startIdx;
        if (count < 2)
            return null;

        var duration = points[endIdx - 1].Time - points[startIdx].Time;
        if (duration < cfg.MinDuration)
            return null;

        // Reject if the person actually moved too far
        if (cfg.MaxDistance > 0)
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
            Lon = sumLon / n
        };
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
