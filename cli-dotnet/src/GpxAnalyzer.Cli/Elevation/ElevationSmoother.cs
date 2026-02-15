using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Elevation;

public static class ElevationSmoother
{
    /// <summary>
    /// Minimum time gap indicating a discontinuity (overnight camp, segment boundary).
    /// </summary>
    public static readonly TimeSpan GapThreshold = TimeSpan.FromMinutes(10);

    public static readonly Dictionary<string, SmoothingParams> Presets = new()
    {
        ["none"] = new(0, 0),
        ["light"] = new(3, 3),
        ["medium"] = new(5, 5),
        ["heavy"] = new(7, 11),
    };

    public static bool IsValidLevel(string level) => Presets.ContainsKey(level);

    /// <summary>
    /// Applies median then moving average filters to the Ele field of points, in place.
    /// Smoothing is applied independently within time-continuous segments.
    /// </summary>
    public static void SmoothElevations(List<TrackPoint> points, string level)
    {
        if (!Presets.TryGetValue(level, out var p) || level == "none")
            return;

        var times = ExtractTimes(points);
        var breaks = GapIndices(times, GapThreshold);

        var elevations = ExtractElevations(points);
        elevations = MedianFilterSegmented(elevations, p.MedianWindow, breaks);
        elevations = MovingAverageSegmented(elevations, p.AverageWindow, breaks);
        ApplyElevations(points, elevations);
    }

    internal static double[] MedianFilter(double[] data, int windowSize)
    {
        if (windowSize <= 1 || data.Length == 0)
            return data;

        var result = new double[data.Length];
        int half = windowSize / 2;

        for (int i = 0; i < data.Length; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(data.Length - 1, i + half);
            var window = new double[end - start + 1];
            Array.Copy(data, start, window, 0, window.Length);
            Array.Sort(window);
            result[i] = window[window.Length / 2];
        }
        return result;
    }

    internal static double[] MovingAverage(double[] data, int windowSize)
    {
        if (windowSize <= 1 || data.Length == 0)
            return data;

        var result = new double[data.Length];
        int half = windowSize / 2;

        for (int i = 0; i < data.Length; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(data.Length - 1, i + half);
            double sum = 0;
            for (int j = start; j <= end; j++)
                sum += data[j];
            result[i] = sum / (end - start + 1);
        }
        return result;
    }

    internal static DateTime[] ExtractTimes(List<TrackPoint> points)
    {
        var times = new DateTime[points.Count];
        for (int i = 0; i < points.Count; i++)
            times[i] = points[i].Time;
        return times;
    }

    internal static int[] GapIndices(DateTime[] times, TimeSpan threshold)
    {
        var breaks = new List<int>();
        for (int i = 1; i < times.Length; i++)
        {
            if (times[i] - times[i - 1] > threshold)
                breaks.Add(i);
        }
        return breaks.ToArray();
    }

    private static double[] ExtractElevations(List<TrackPoint> points)
    {
        var elev = new double[points.Count];
        for (int i = 0; i < points.Count; i++)
            elev[i] = points[i].Ele;
        return elev;
    }

    private static void ApplyElevations(List<TrackPoint> points, double[] elev)
    {
        for (int i = 0; i < points.Count; i++)
            points[i].Ele = elev[i];
    }

    internal static double[] MovingAverageSegmented(double[] data, int windowSize, int[] breaks)
    {
        if (windowSize <= 1 || data.Length == 0)
            return data;
        if (breaks.Length == 0)
            return MovingAverage(data, windowSize);

        var result = new double[data.Length];
        int start = 0;
        foreach (int brk in breaks)
        {
            var segment = data[start..brk];
            var smoothed = MovingAverage(segment, windowSize);
            Array.Copy(smoothed, 0, result, start, smoothed.Length);
            start = brk;
        }
        var last = data[start..];
        var lastSmoothed = MovingAverage(last, windowSize);
        Array.Copy(lastSmoothed, 0, result, start, lastSmoothed.Length);
        return result;
    }

    internal static double[] MedianFilterSegmented(double[] data, int windowSize, int[] breaks)
    {
        if (windowSize <= 1 || data.Length == 0)
            return data;
        if (breaks.Length == 0)
            return MedianFilter(data, windowSize);

        var result = new double[data.Length];
        int start = 0;
        foreach (int brk in breaks)
        {
            var segment = data[start..brk];
            var filtered = MedianFilter(segment, windowSize);
            Array.Copy(filtered, 0, result, start, filtered.Length);
            start = brk;
        }
        var last = data[start..];
        var lastFiltered = MedianFilter(last, windowSize);
        Array.Copy(lastFiltered, 0, result, start, lastFiltered.Length);
        return result;
    }
}

public readonly record struct SmoothingParams(int MedianWindow, int AverageWindow);
