using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Split;

namespace GpxAnalyzer.Cli.Tests.Split;

public class TimeSplitterTests
{
    private static TrackPoint P(double lat, DateTime t) =>
        new() { Lat = lat, Lon = 2.0, Ele = 100, Time = t };

    [Fact]
    public void ByTime_UntimedFirstPoint_DoesNotExplodeIntoBogusSegments()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100 },   // no <time> -> DateTime.MinValue
            P(48.001, t0),
            P(48.002, t0.AddMinutes(1)),
        };

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(24));

        // Before the fix this returns 738,886 segments and ~700 MB of allocations.
        Assert.Single(segments);
        Assert.Equal(3, segments[0].Points.Count);
    }

    [Fact]
    public void ByTime_RecordingGapLongerThanInterval_DoesNotEmitDuplicateOnePointSegments()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint> { P(48.0, t0), P(48.01, t0.AddHours(3)) };

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(1));

        // Before the fix: 4 segments, three of them holding the same single point.
        Assert.All(segments, s => Assert.True(s.Points.Count >= 2,
            $"segment {s.Index} holds only {s.Points.Count} point(s)"));
        Assert.True(segments.Count <= 2, $"expected at most 2 segments, got {segments.Count}");
    }

    [Fact]
    public void ByTime_BoundaryPointIsCloned_SoMutatingOneSegmentDoesNotAffectTheNext()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int i = 0; i < 6; i++) points.Add(P(48.0 + i * 0.001, t0.AddMinutes(i * 20)));

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(1));
        Assert.True(segments.Count >= 2, "fixture must produce at least two segments");

        // SplitCommand writes segment i, then runs ComputePipeline on it, which
        // mutates Ele in place. That must not reach segment i+1's first point.
        var tail = segments[0].Points[^1];
        var head = segments[1].Points[0];
        Assert.NotSame(tail, head);

        tail.Ele = 9999;
        Assert.NotEqual(9999, head.Ele);
    }

    [Fact]
    public void ByTime_NormalMultiDayTrack_SplitsOnePerDay()
    {
        var t0 = DateTime.Parse("2024-01-01T08:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int day = 0; day < 3; day++)
            for (int i = 0; i < 10; i++)
                points.Add(P(48.0 + i * 0.001, t0.AddDays(day).AddMinutes(i * 10)));

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(24));

        Assert.Equal(3, segments.Count);
        Assert.All(segments, s => Assert.True(s.Points.Count >= 10));
    }
}
