using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class GpsFilterTests
{
    [Fact]
    public void FilterOutliers_NoOutliers_ReturnsSameCount()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Lat = 48.0001, Lon = 2.0001, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
            new() { Lat = 48.0002, Lon = 2.0002, Time = DateTime.Parse("2024-01-01T10:02:00Z").ToUniversalTime() },
        };
        var (filtered, removed) = GpsFilter.FilterOutliers(points, 25.0);
        Assert.Equal(0, removed);
        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public void FilterOutliers_WithOutlier_RemovesIt()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Lat = 49.0, Lon = 3.0, Time = DateTime.Parse("2024-01-01T10:00:01Z").ToUniversalTime() },
            new() { Lat = 48.0002, Lon = 2.0002, Time = DateTime.Parse("2024-01-01T10:02:00Z").ToUniversalTime() },
        };
        var (filtered, removed) = GpsFilter.FilterOutliers(points, 25.0);
        Assert.Equal(1, removed);
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void FilterOutliers_EmptyList_ReturnsZero()
    {
        var points = new List<TrackPoint>();
        var (filtered, removed) = GpsFilter.FilterOutliers(points, 25.0);
        Assert.Equal(0, removed);
        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterOutliers_SinglePoint_ReturnsZero()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
        };
        var (filtered, removed) = GpsFilter.FilterOutliers(points, 25.0);
        Assert.Equal(0, removed);
        Assert.Single(filtered);
    }

    [Fact]
    public void FilterOutliers_BadFirstFixAtZeroZero_DoesNotDeleteTheWholeTrack()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>
        {
            // Cheap receiver's first fix: lat/lon attributes missing -> parsed as 0,0
            new() { Lat = 0.0, Lon = 0.0, Time = t0 },
        };
        // 50 real points near 48.0/2.0, ~7 m apart, one every 5 s (1.3 m/s, a hiking pace)
        for (int i = 1; i <= 50; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.00006,
                Lon = 2.0,
                Time = t0.AddSeconds(i * 5),
            });

        var (filtered, removed) = GpsFilter.FilterOutliers(points, 4.0); // hiking preset

        // The single bad anchor must not eat the activity.
        Assert.True(filtered.Count >= 45,
            $"expected the real track to survive, kept only {filtered.Count} of 51");
        Assert.DoesNotContain(filtered, p => p.Lat == 0.0 && p.Lon == 0.0);
        Assert.True(removed <= 6, $"expected a handful of removals, got {removed}");
    }

    [Fact]
    public void FilterOutliers_SingleMidTrackSpike_StillRemovesOnlyTheSpike()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int i = 0; i < 20; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.00006, Lon = 2.0, Time = t0.AddSeconds(i * 5) });
        // One teleport at index 10
        points[10] = new TrackPoint { Lat = 49.5, Lon = 3.5, Time = t0.AddSeconds(50) };

        var (filtered, removed) = GpsFilter.FilterOutliers(points, 4.0);

        Assert.Equal(1, removed);
        Assert.Equal(19, filtered.Count);
    }
}
