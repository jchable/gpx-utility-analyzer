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
        var baseTime = points[0].Time;
        int segIndex = 0;
        var currentPoints = new List<TrackPoint>();
        var segStart = baseTime;
        var segEnd = baseTime + interval;

        foreach (var p in points)
        {
            // Move to the correct bucket
            while (p.Time >= segEnd)
            {
                if (currentPoints.Count > 0)
                {
                    // Duplicate boundary point into next segment
                    var lastPoint = currentPoints[^1];
                    segments.Add(new TimeSegment
                    {
                        Index = segIndex,
                        StartTime = segStart,
                        EndTime = segEnd,
                        Points = new List<TrackPoint>(currentPoints)
                    });
                    segIndex++;
                    currentPoints = [lastPoint]; // boundary duplication
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
