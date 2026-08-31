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
        var segStart = baseTime;
        var segEnd = baseTime + interval;

        foreach (var p in points)
        {
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
                }

                segStart = segEnd;
                segEnd = segStart + interval;
            }

            currentPoints.Add(p);
        }

        // Final segment
        if (currentPoints.Count > 0)
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
