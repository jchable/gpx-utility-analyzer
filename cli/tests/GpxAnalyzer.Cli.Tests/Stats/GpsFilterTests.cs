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
}
