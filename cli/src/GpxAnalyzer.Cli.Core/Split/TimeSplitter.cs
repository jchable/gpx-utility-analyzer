using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Split;

public sealed class TimeSegment
{
    public int Index { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public List<TrackPoint> Points { get; init; } = [];
}

public static class TimeSplitter
{
    public static List<TimeSegment> ByTime(List<TrackPoint> points, TimeSpan interval)
    {
        if (points.Count == 0)
            throw new InvalidOperationException("No points to split");
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be positive", nameof(interval));

        var segments = new List<TimeSegment>();

        // A trkpt with no <time> parses as DateTime.MinValue. Anchoring the bucket
        // window there makes the catch-up loop walk two millennia one interval at
        // a time, emitting a junk segment per iteration. Anchor on the first
        // timestamp that is actually usable instead.
        var baseTime = points.FirstOrDefault(p => p.Time > DateTime.MinValue)?.Time
                       ?? points[0].Time;

        int segIndex = 0;
        var currentPoints = new List<TrackPoint>();
        var hasRetainedBoundary = false;
        TrackPoint? previousPoint = null;
        var segStart = baseTime;
        var segEnd = baseTime + interval;

        foreach (var p in points)
        {
            var startsAfterGap = previousPoint != null &&
                p.Time - previousPoint.Time > ElevationSmoother.GapThreshold;

            // Move to the correct bucket
            while (p.Time >= segEnd)
            {
                // Only emit a bucket that actually holds recorded points. After a
                // flush, currentPoints holds nothing but the duplicated boundary
                // point, so a multi-interval recording gap would otherwise emit one
                // junk single-point segment per interval it spans.
                if (currentPoints.Count > 1)
                {
                    var lastPoint = currentPoints[^1];
                    segments.Add(new TimeSegment
                    {
                        Index = segIndex,
                        StartTime = segStart,
                        EndTime = segEnd,
                        Points = new List<TrackPoint>(currentPoints)
                    });
                    segIndex++;

                    // Clone: consumers (SplitCommand) run ComputePipeline per
                    // segment, which mutates Ele/Lat/Lon in place. Sharing the
                    // boundary object writes one segment's smoothed values into
                    // the neighbouring segment's exported GPX.
                    currentPoints = [lastPoint.Clone()];
                    hasRetainedBoundary = true;
                }

                segStart = segEnd;
                segEnd = segStart + interval;
            }

            // A new GPX segment has no path back to the retained boundary point.
            // Keeping that point would make the next output span the whole recording
            // gap and would fabricate moving time and elevation across it.
            if (hasRetainedBoundary && (p.StartsNewSegment || startsAfterGap))
                currentPoints.Clear();

            currentPoints.Add(p);
            hasRetainedBoundary = false;
            previousPoint = p;
        }

        // Final segment. The boundary clear above can fire on the very last point, leaving a
        // trailing block holding nothing but that one point - the same junk the in-loop guard
        // rejects, and a split with no distance and no time. A single-point INPUT is not junk
        // though: there is nothing else it could ever produce.
        if (currentPoints.Count > 1 || (currentPoints.Count == 1 && segments.Count == 0))
        {
            segments.Add(new TimeSegment
            {
                Index = segIndex,
                StartTime = segStart,
                EndTime = segEnd,
                Points = currentPoints
            });
        }

        return segments;
    }
}
