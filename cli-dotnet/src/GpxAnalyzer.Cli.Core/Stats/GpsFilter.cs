using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

public static class GpsFilter
{
    /// <summary>
    /// Removes GPS outlier points whose speed from the last accepted point exceeds maxSpeed (m/s).
    /// Forward-scan algorithm: point 0 is always kept as anchor.
    /// Returns the filtered list and the number of removed points.
    /// </summary>
    public static (List<TrackPoint> Filtered, int Removed) FilterOutliers(
        List<TrackPoint> points, double maxSpeed)
    {
        if (maxSpeed <= 0 || points.Count <= 1)
            return (points, 0);

        var filtered = new List<TrackPoint>(points.Count) { points[0] };
        int removed = 0;

        for (int i = 1; i < points.Count; i++)
        {
            var anchor = filtered[^1];
            double dt = (points[i].Time - anchor.Time).TotalSeconds;

            if (dt <= 0)
            {
                // Can't compute speed — keep the point (simultaneous timestamps)
                filtered.Add(points[i]);
                continue;
            }

            double dist = DistanceCalculator.Haversine(
                anchor.Lat, anchor.Lon, points[i].Lat, points[i].Lon);
            double speed = dist / dt;

            if (speed > maxSpeed)
            {
                removed++;
                continue;
            }

            filtered.Add(points[i]);
        }

        return (filtered, removed);
    }
}
