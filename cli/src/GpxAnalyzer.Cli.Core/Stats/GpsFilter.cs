using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

public static class GpsFilter
{
    /// <summary>
    /// After this many consecutive rejections we assume the anchor itself is the
    /// outlier (a bad first fix at 0,0 is the common case) and re-anchor onto the
    /// current point rather than deleting the remainder of the track.
    /// </summary>
    public const int MaxConsecutiveRejections = 3;

    /// <summary>
    /// Removes GPS outlier points whose speed from the last accepted point exceeds maxSpeed (m/s).
    /// Forward-scan algorithm: point 0 is the initial anchor, but an anchor that rejects
    /// MaxConsecutiveRejections points in a row is itself discarded.
    /// Returns the filtered list and the number of removed points.
    /// </summary>
    public static (List<TrackPoint> Filtered, int Removed) FilterOutliers(
        List<TrackPoint> points, double maxSpeed)
    {
        if (maxSpeed <= 0 || points.Count <= 1)
            return (points, 0);

        var filtered = new List<TrackPoint>(points.Count) { points[0] };
        int removed = 0;
        int consecutiveRejections = 0;

        for (int i = 1; i < points.Count; i++)
        {
            var anchor = filtered[^1];
            double dt = (points[i].Time - anchor.Time).TotalSeconds;

            if (dt <= 0)
            {
                // Can't compute speed — keep the point (simultaneous timestamps)
                filtered.Add(points[i]);
                consecutiveRejections = 0;
                continue;
            }

            double dist = DistanceCalculator.Haversine(
                anchor.Lat, anchor.Lon, points[i].Lat, points[i].Lon);
            double speed = dist / dt;

            if (speed > maxSpeed)
            {
                consecutiveRejections++;

                if (consecutiveRejections >= MaxConsecutiveRejections)
                {
                    // The anchor, not the stream of points, is the outlier.
                    // Drop the anchor and restart from here.
                    filtered.RemoveAt(filtered.Count - 1);
                    removed++;                            // the discarded anchor
                    removed -= consecutiveRejections - 1; // un-count points rejected against it
                    filtered.Add(points[i]);
                    consecutiveRejections = 0;
                    continue;
                }

                removed++;
                continue;
            }

            filtered.Add(points[i]);
            consecutiveRejections = 0;
        }

        return (filtered, removed);
    }
}
