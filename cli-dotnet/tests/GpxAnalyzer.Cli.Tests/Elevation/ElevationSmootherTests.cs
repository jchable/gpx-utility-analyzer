using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Tests.Elevation;

public class ElevationSmootherTests
{
    private static List<TrackPoint> MakePoints(params double[] elevations)
    {
        return elevations.Select((e, i) => new TrackPoint
        {
            Lat = 48.0 + i * 0.001,
            Lon = 2.0 + i * 0.001,
            Ele = e,
            Time = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddMinutes(i)
        }).ToList();
    }

    [Fact]
    public void SmoothElevations_NoneLevel_NoChange()
    {
        var points = MakePoints(100, 200, 300, 400, 500);
        var original = points.Select(p => p.Ele).ToArray();
        ElevationSmoother.SmoothElevations(points, "none");
        for (int i = 0; i < points.Count; i++)
            Assert.Equal(original[i], points[i].Ele);
    }

    [Fact]
    public void SmoothElevations_MediumLevel_Smooths()
    {
        // Use enough points for the window size (medium = 5 median + 5 avg)
        var points = MakePoints(100, 200, 50, 200, 100, 200, 50, 200, 100, 200, 50);
        var original = points.Select(p => p.Ele).ToArray();
        ElevationSmoother.SmoothElevations(points, "medium");
        // After smoothing, at least some values should be different
        bool anyDifferent = false;
        for (int i = 0; i < points.Count; i++)
        {
            if (Math.Abs(points[i].Ele - original[i]) > 0.001)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Smoothing should modify at least some elevation values");
        // Range should not increase
        double maxAfter = points.Max(p => p.Ele);
        double minAfter = points.Min(p => p.Ele);
        Assert.True(maxAfter <= 200.01 && minAfter >= 49.99);
    }

    [Fact]
    public void IsValidLevel_ValidLevels()
    {
        Assert.True(ElevationSmoother.IsValidLevel("none"));
        Assert.True(ElevationSmoother.IsValidLevel("light"));
        Assert.True(ElevationSmoother.IsValidLevel("medium"));
        Assert.True(ElevationSmoother.IsValidLevel("heavy"));
    }

    [Fact]
    public void IsValidLevel_InvalidLevel()
    {
        Assert.False(ElevationSmoother.IsValidLevel("extreme"));
        Assert.False(ElevationSmoother.IsValidLevel(""));
    }
}
