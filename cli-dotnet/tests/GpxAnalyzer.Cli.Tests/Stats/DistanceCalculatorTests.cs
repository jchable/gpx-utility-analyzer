using GpxAnalyzer.Cli.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class DistanceCalculatorTests
{
    [Fact]
    public void Haversine_SamePoint_ReturnsZero()
    {
        double d = DistanceCalculator.Haversine(48.8566, 2.3522, 48.8566, 2.3522);
        Assert.Equal(0.0, d, 1);
    }

    [Fact]
    public void Haversine_KnownDistance_ParisToLyon()
    {
        // Paris (48.8566, 2.3522) to Lyon (45.7640, 4.8357)
        double d = DistanceCalculator.Haversine(48.8566, 2.3522, 45.7640, 4.8357);
        // ~392 km
        Assert.InRange(d, 390_000, 395_000);
    }

    [Fact]
    public void Haversine_ShortDistance_InMeters()
    {
        // Two nearby points in Paris
        double d = DistanceCalculator.Haversine(48.8566, 2.3522, 48.8580, 2.3540);
        // Should be roughly 200m
        Assert.InRange(d, 150, 250);
    }

    [Fact]
    public void Distance3D_AddsElevation()
    {
        double d2d = DistanceCalculator.Haversine(48.8566, 2.3522, 48.8580, 2.3540);
        double d3d = DistanceCalculator.Distance3D(48.8566, 2.3522, 100.0, 48.8580, 2.3540, 200.0);
        Assert.True(d3d > d2d);
        // With 100m elevation difference, 3D distance should be longer
        double expected = Math.Sqrt(d2d * d2d + 100.0 * 100.0);
        Assert.Equal(expected, d3d, 1);
    }

    [Fact]
    public void Distance3D_FlatTerrain_EqualTo2D()
    {
        double d2d = DistanceCalculator.Haversine(48.8566, 2.3522, 48.8580, 2.3540);
        double d3d = DistanceCalculator.Distance3D(48.8566, 2.3522, 100.0, 48.8580, 2.3540, 100.0);
        Assert.Equal(d2d, d3d, 1);
    }
}
