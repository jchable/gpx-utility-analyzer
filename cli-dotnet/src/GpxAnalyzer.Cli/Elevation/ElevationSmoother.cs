using System.Buffers;
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
        => MedianFilterRange(data, 0, data.Length, windowSize);

    internal static void MedianFilterRange(double[] data, int offset, int length,
        int windowSize, double[] result)
    {
        if (windowSize <= 1 || length == 0)
        {
            Array.Copy(data, offset, result, offset, length);
            return;
        }

        int half = windowSize / 2;
        int maxWindow = windowSize + 1;
        var window = ArrayPool<double>.Shared.Rent(maxWindow);
        try
        {
            int end = offset + length;
            for (int i = offset; i < end; i++)
            {
                int wStart = Math.Max(offset, i - half);
                int wEnd = Math.Min(end - 1, i + half);
                int wLen = wEnd - wStart + 1;
                Array.Copy(data, wStart, window, 0, wLen);
                Array.Sort(window, 0, wLen);
                result[i] = window[wLen / 2];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(window);
        }
    }

    private static double[] MedianFilterRange(double[] data, int offset, int length, int windowSize)
    {
        if (windowSize <= 1 || length == 0)
            return data;

        var result = new double[data.Length];
        MedianFilterRange(data, offset, length, windowSize, result);
        return result;
    }

    internal static double[] MovingAverage(double[] data, int windowSize)
        => MovingAverageRange(data, 0, data.Length, windowSize);

    internal static void MovingAverageRange(double[] data, int offset, int length,
        int windowSize, double[] result)
    {
        if (windowSize <= 1 || length == 0)
        {
            Array.Copy(data, offset, result, offset, length);
            return;
        }

        int half = windowSize / 2;
        int end = offset + length;
        for (int i = offset; i < end; i++)
        {
            int wStart = Math.Max(offset, i - half);
            int wEnd = Math.Min(end - 1, i + half);
            double sum = 0;
            for (int j = wStart; j <= wEnd; j++)
                sum += data[j];
            result[i] = sum / (wEnd - wStart + 1);
        }
    }

    private static double[] MovingAverageRange(double[] data, int offset, int length, int windowSize)
    {
        if (windowSize <= 1 || length == 0)
            return data;

        var result = new double[data.Length];
        MovingAverageRange(data, offset, length, windowSize, result);
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
            MovingAverageRange(data, start, brk - start, windowSize, result);
            start = brk;
        }
        MovingAverageRange(data, start, data.Length - start, windowSize, result);
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
            MedianFilterRange(data, start, brk - start, windowSize, result);
            start = brk;
        }
        MedianFilterRange(data, start, data.Length - start, windowSize, result);
        return result;
    }
}

public readonly record struct SmoothingParams(int MedianWindow, int AverageWindow);
