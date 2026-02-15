using GpxAnalyzer.Cli.Elevation;
using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Tests.Elevation;

public class TrackSmootherTests
{
    [Fact]
    public void SmoothTrack_NoneLevel_NoChange()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0 },
            new() { Lat = 48.1, Lon = 2.1 },
            new() { Lat = 48.2, Lon = 2.2 },
        };
        var origLats = points.Select(p => p.Lat).ToArray();
        TrackSmoother.SmoothTrack(points, "none");
        for (int i = 0; i < points.Count; i++)
            Assert.Equal(origLats[i], points[i].Lat);
    }

    [Fact]
    public void IsValidLevel_ValidLevels()
    {
        Assert.True(TrackSmoother.IsValidLevel("none"));
        Assert.True(TrackSmoother.IsValidLevel("light"));
        Assert.True(TrackSmoother.IsValidLevel("medium"));
        Assert.True(TrackSmoother.IsValidLevel("heavy"));
    }

    [Fact]
    public void IsValidLevel_InvalidLevel()
    {
        Assert.False(TrackSmoother.IsValidLevel("super"));
    }
}
