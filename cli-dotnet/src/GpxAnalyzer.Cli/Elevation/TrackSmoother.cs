using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Elevation;

public static class TrackSmoother
{
    private static readonly Dictionary<string, int> Windows = new()
    {
        ["none"] = 0,
        ["light"] = 3,
        ["medium"] = 5,
        ["heavy"] = 9,
    };

    public static bool IsValidLevel(string level) => Windows.ContainsKey(level);

    /// <summary>
    /// Applies moving average to Lat and Lon fields, reducing horizontal GPS noise.
    /// Applied independently within time-continuous segments.
    /// </summary>
    public static void SmoothTrack(List<TrackPoint> points, string level)
    {
        if (!Windows.TryGetValue(level, out int window) || window <= 1)
            return;

        var times = ElevationSmoother.ExtractTimes(points);
        var breaks = ElevationSmoother.GapIndices(times, ElevationSmoother.GapThreshold);

        var lats = new double[points.Count];
        var lons = new double[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            lats[i] = points[i].Lat;
            lons[i] = points[i].Lon;
        }

        lats = ElevationSmoother.MovingAverageSegmented(lats, window, breaks);
        lons = ElevationSmoother.MovingAverageSegmented(lons, window, breaks);

        for (int i = 0; i < points.Count; i++)
        {
            points[i].Lat = lats[i];
            points[i].Lon = lons[i];
        }
    }
}
