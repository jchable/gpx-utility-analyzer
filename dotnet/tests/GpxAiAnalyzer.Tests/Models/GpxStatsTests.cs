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

    private static GpxStats LoadFixture()
    {
        var json = File.ReadAllText(FixturePath);
        return JsonSerializer.Deserialize<GpxStats>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }
}
