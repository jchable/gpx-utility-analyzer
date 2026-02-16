using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class BiometricsCalculatorTests
{
    [Fact]
    public void Compute_WithHeartRate_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { HeartRate = 120, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { HeartRate = 140, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
            new() { HeartRate = 160, Time = DateTime.Parse("2024-01-01T10:02:00Z").ToUniversalTime() },
            new() { HeartRate = 150, Time = DateTime.Parse("2024-01-01T10:03:00Z").ToUniversalTime() },
        };
        var cfg = new BiometricsConfig { MaxHR = 190 };
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.HeartRate);
        Assert.Equal(160, result.HeartRate!.Max);
        Assert.Equal(120, result.HeartRate.Min);
        Assert.True(result.HeartRate.Avg > 0);
        Assert.Equal(5, result.HeartRate.Zones.Count);
    }

    [Fact]
    public void Compute_NoHeartRate_ReturnsNull()
    {
        var points = new List<TrackPoint>
        {
            new() { Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
        };
        var cfg = new BiometricsConfig { MaxHR = 190 };
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.Null(result.HeartRate);
    }

    [Fact]
    public void Compute_WithPower_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { Power = 200, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Power = 220, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
            new() { Power = 250, Time = DateTime.Parse("2024-01-01T10:02:00Z").ToUniversalTime() },
        };
        var cfg = new BiometricsConfig();
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.Power);
        Assert.Equal(250, result.Power!.Max);
        Assert.True(result.Power.Avg > 0);
        Assert.True(result.Power.NormalizedPower >= 0);
    }

    [Fact]
    public void Compute_WithCadence_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { Cadence = 80 },
            new() { Cadence = 90 },
            new() { Cadence = 85 },
        };
        var cfg = new BiometricsConfig();
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.Cadence);
        Assert.Equal(90, result.Cadence!.Max);
        Assert.True(result.Cadence.Avg > 0);
    }

    [Fact]
    public void Compute_WithTemperature_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { Temperature = 18.0 },
            new() { Temperature = 20.0 },
            new() { Temperature = 22.0 },
        };
        var cfg = new BiometricsConfig();
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.Temperature);
        Assert.Equal(22.0, result.Temperature!.Max, 0.1);
        Assert.Equal(18.0, result.Temperature.Min, 0.1);
    }
}
