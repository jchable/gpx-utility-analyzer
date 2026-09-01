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

    [Fact]
    public void ByTime_NewGpxSegmentDoesNotCarryOldBoundaryAcrossGap()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>
        {
            P(48.0, t0),
            P(48.001, t0.AddMinutes(5)),
            new() { Lat = 45.0, Lon = 4.0, Ele = 200, Time = t0.AddDays(1), StartsNewSegment = true },
            P(45.001, t0.AddDays(1).AddMinutes(5)),
        };

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(12));

        Assert.Equal(2, segments.Count);
        Assert.Equal(t0.AddDays(1), segments[1].Points[0].Time);
        Assert.Equal(2, segments[1].Points.Count);
    }

    [Fact]
    public void ByTime_LongGapInSameGpxSegmentDoesNotCarryOldBoundary()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>
        {
            P(48.0, t0),
            P(48.001, t0.AddMinutes(5)),
            P(45.0, t0.AddDays(1)),
            P(45.001, t0.AddDays(1).AddMinutes(5)),
        };

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromHours(12));

        Assert.Equal(2, segments.Count);
        Assert.Equal(t0.AddDays(1), segments[1].Points[0].Time);
        Assert.Equal(2, segments[1].Points.Count);
    }

    /// <summary>
    /// The in-loop flush refuses to emit a bucket holding nothing but the retained boundary
    /// point. The trailing block had no such guard, so when the boundary clear fired on the
    /// very last point - the retained boundary dropped, that one point kept - the run ended
    /// with a one-point split: no distance, no time, every statistic zero.
    /// </summary>
    [Theory]
    [InlineData(true)]      // a new <trkseg> opens on the last point
    [InlineData(false)]     // ... or a recording gap does
    public void ByTime_BoundaryClearOnTheLastPoint_DoesNotEmitAOnePointSegment(bool structural)
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var last = structural
            ? new TrackPoint { Lat = 45.0, Lon = 2.0, Ele = 100, Time = t0.AddMinutes(35), StartsNewSegment = true }
            : P(45.0, t0.AddMinutes(45));   // 25 min after the previous point: a recording gap

        var points = new List<TrackPoint>
        {
            P(48.000, t0),
            P(48.001, t0.AddMinutes(10)),
            P(48.002, t0.AddMinutes(20)),
            last,
        };

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromMinutes(30));

        Assert.Single(segments);
        Assert.Equal(3, segments[0].Points.Count);
        Assert.DoesNotContain(segments, s => s.Points.Count == 1);
    }

    /// <summary>
    /// The guard must not swallow a genuine trailing bucket, nor a single-point input.
    /// </summary>
    [Fact]
    public void ByTime_TrailingBucketWithRealPoints_IsStillEmitted()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        // 5-minute spacing throughout: the bucket change is not a recording gap, so the
        // boundary point is retained rather than cleared.
        var points = new List<TrackPoint>
        {
            P(48.000, t0),
            P(48.001, t0.AddMinutes(10)),
            P(48.002, t0.AddMinutes(20)),
            P(48.003, t0.AddMinutes(25)),
            P(48.004, t0.AddMinutes(32)),
            P(48.005, t0.AddMinutes(36)),
        };

        var segments = TimeSplitter.ByTime(points, TimeSpan.FromMinutes(30));

        Assert.Equal(2, segments.Count);
        Assert.Equal(4, segments[0].Points.Count);
        Assert.Equal(3, segments[1].Points.Count);   // retained boundary + the two real points
    }

    [Fact]
    public void ByTime_SinglePointInput_StillYieldsThatPoint()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();

        var segments = TimeSplitter.ByTime([P(48.0, t0)], TimeSpan.FromHours(24));

        Assert.Single(segments);
        Assert.Single(segments[0].Points);
    }
}
