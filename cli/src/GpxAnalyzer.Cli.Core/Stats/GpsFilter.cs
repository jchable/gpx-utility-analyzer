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
    ///
    /// <para>
    /// <see cref="TrackPoint.StartsNewSegment"/> is structural and source-owned: it says the
    /// input file opened a &lt;trkseg&gt; at this point, and every compute stage downstream
    /// only reads it. This pass is the one exception, and only because it deletes points -
    /// dropping the point that carried a boundary must move the boundary onto whichever point
    /// now begins that segment, never erase it. The fact belongs to the file, not to the
    /// particular fix that happened to be first (issue #142).
    /// </para>
    /// <para>
    /// It is the segment-opening points that need this most: the first fix after a device
    /// resumes recording is often bad, and its apparent speed is enormous precisely because it
    /// spans the pause - which is exactly what makes this filter reject it.
    /// </para>
    /// </summary>
    public static (List<TrackPoint> Filtered, int Removed) FilterOutliers(
        List<TrackPoint> points, double maxSpeed)
    {
        if (maxSpeed <= 0 || points.Count <= 1)
            return (points, 0);

        var filtered = new List<TrackPoint>(points.Count) { points[0] };
        int removed = 0;
        int consecutiveRejections = 0;

        // A boundary belonging to a point we dropped, waiting for the point that inherits it.
        bool pendingSegmentStart = false;

        for (int i = 1; i < points.Count; i++)
        {
            var anchor = filtered[^1];
            double dt = (points[i].Time - anchor.Time).TotalSeconds;

            if (dt <= 0)
            {
                // Can't compute speed — keep the point (simultaneous timestamps)
                Keep(points[i]);
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
                    // The anchor is going too, and it may itself have opened a segment (or
                    // have inherited one from an earlier drop).
                    pendingSegmentStart |= anchor.StartsNewSegment;
                    Keep(points[i]);
                    consecutiveRejections = 0;
                    continue;
                }

                removed++;
                pendingSegmentStart |= points[i].StartsNewSegment;
                continue;
            }

            Keep(points[i]);
            consecutiveRejections = 0;
        }

        return (filtered, removed);

        void Keep(TrackPoint p)
        {
            if (pendingSegmentStart)
            {
                p.StartsNewSegment = true;
                pendingSegmentStart = false;
            }
            filtered.Add(p);
        }
    }
}
