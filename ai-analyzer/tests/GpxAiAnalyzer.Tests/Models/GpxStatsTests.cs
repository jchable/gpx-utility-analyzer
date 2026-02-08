namespace GpxAiAnalyzer.Tests.Models;

using GpxAiAnalyzer.Models;
using System.Text.Json;

public class GpxStatsTests
{
    private static readonly string FixturePath = Path.Combine("testdata", "sample-stats.json");

    [Fact]
    public void Deserialize_SampleJson_ReturnsCorrectFilename()
    {
        var stats = LoadFixture();
        Assert.Equal("testdata/two-segments.gpx", stats.Filename);
    }

    [Fact]
    public void Deserialize_SampleJson_ReturnsCorrectDistances()
    {
        var stats = LoadFixture();
        Assert.Equal(736.306, stats.TotalDistanceM, precision: 0);
        Assert.Equal(0.736, stats.TotalDistanceKm, precision: 2);
        Assert.True(stats.TotalDistance3dM >= stats.TotalDistanceM);
    }

    [Fact]
    public void Deserialize_SampleJson_ReturnsCorrectElevation()
    {
        var stats = LoadFixture();
        Assert.Equal(47, stats.MaxElevationM, precision: 0);
        Assert.True(stats.MinElevationM > 46);
    }

    [Fact]
    public void Deserialize_SampleJson_ReturnsDurationValues()
    {
        var stats = LoadFixture();
        Assert.Equal(86700, stats.TotalTime.Seconds);
        Assert.Equal("1d 0h 5m 0s", stats.TotalTime.Display);
        Assert.Equal(600, stats.MovingTime.Seconds);
    }

    [Fact]
    public void Deserialize_SampleJson_ReturnsStops()
    {
        var stats = LoadFixture();
        Assert.Equal(1, stats.StopCount);
        Assert.NotNull(stats.Stops);
        Assert.Single(stats.Stops);
        Assert.NotNull(stats.LongestStop);
        Assert.Equal(86100, stats.LongestStop.Duration.Seconds);
        Assert.InRange(stats.LongestStop.Lat, 48.85, 48.86);
    }

    [Fact]
    public void Deserialize_SampleJson_ReturnsSpeedAndPace()
    {
        var stats = LoadFixture();
        Assert.True(stats.AvgMovingSpeedKmh > stats.AvgSpeedKmh);
        Assert.Equal("13:34 min/km", stats.AvgMovingPace);
    }

    [Fact]
    public void Deserialize_SampleJson_ReturnsPointMetadata()
    {
        var stats = LoadFixture();
        Assert.Equal(4, stats.PointCount);
        Assert.Equal(2, stats.SegmentCount);
    }

    [Fact]
    public void Deserialize_SampleJson_NoBiometrics_ReturnsNull()
    {
        var stats = LoadFixture();
        Assert.Null(stats.HeartRate);
        Assert.Null(stats.Power);
        Assert.Null(stats.Cadence);
        Assert.Null(stats.Temperature);
    }

    [Fact]
    public void Deserialize_BiometricsJson_ReturnsHeartRate()
    {
        var stats = LoadBiometricsFixture();
        Assert.NotNull(stats.HeartRate);
        Assert.Equal(170, stats.HeartRate.MaxBpm);
        Assert.Equal(120, stats.HeartRate.MinBpm);
        Assert.True(stats.HeartRate.AvgBpm > 140);
    }

    [Fact]
    public void Deserialize_BiometricsJson_ReturnsHRZones()
    {
        var stats = LoadBiometricsFixture();
        Assert.NotNull(stats.HeartRate?.Zones);
        Assert.Equal(5, stats.HeartRate!.Zones!.Count);
        Assert.Equal("Z3 (Tempo)", stats.HeartRate.Zones[2].Name);
        Assert.Equal(120, stats.HeartRate.Zones[2].Duration.Seconds);
    }

    [Fact]
    public void Deserialize_BiometricsJson_ReturnsPower()
    {
        var stats = LoadBiometricsFixture();
        Assert.NotNull(stats.Power);
        Assert.Equal(280, stats.Power.MaxWatts);
        Assert.Equal(240, stats.Power.AvgWatts, precision: 0);
        Assert.True(stats.Power.NormalizedPowerWatts > 0);
    }

    [Fact]
    public void Deserialize_BiometricsJson_ReturnsCadence()
    {
        var stats = LoadBiometricsFixture();
        Assert.NotNull(stats.Cadence);
        Assert.Equal(95, stats.Cadence.MaxRpm);
        Assert.Equal(88.33, stats.Cadence.AvgRpm, precision: 1);
    }

    [Fact]
    public void Deserialize_BiometricsJson_ReturnsTemperature()
    {
        var stats = LoadBiometricsFixture();
        Assert.NotNull(stats.Temperature);
        Assert.Equal(18.5, stats.Temperature.MinCelsius);
        Assert.Equal(20.5, stats.Temperature.MaxCelsius);
        Assert.Equal(19.55, stats.Temperature.AvgCelsius, precision: 1);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static GpxStats LoadFixture()
    {
        var json = File.ReadAllText(FixturePath);
        return JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
    }

    private static GpxStats LoadBiometricsFixture()
    {
        var json = File.ReadAllText(Path.Combine("testdata", "sample-stats-biometrics.json"));
        return JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
    }
}
