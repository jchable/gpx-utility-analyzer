using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class SpeedCalculatorTests
{
    [Fact]
    public void ComputeSpeed_NormalTrack_ReturnsPositive()
    {
        double dist = 1000;
        var totalTime = TimeSpan.FromMinutes(20);
        var movingTime = TimeSpan.FromMinutes(18);
        var result = SpeedCalculator.ComputeSpeed(dist, totalTime, movingTime);
        Assert.True(result.AvgSpeed > 0);
        Assert.True(result.AvgMovingSpeed > 0);
    }

    [Fact]
    public void ComputeSpeed_ZeroTime_ReturnsZero()
    {
        var result = SpeedCalculator.ComputeSpeed(1000, TimeSpan.Zero, TimeSpan.Zero);
        Assert.Equal(0, result.AvgSpeed);
    }

    [Fact]
    public void EnrichPoints_SetsDistFromPrevAndCalcSpeed()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Lat = 48.001, Lon = 2.001, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
        };
        SpeedCalculator.EnrichPoints(points);
        Assert.Equal(0, points[0].DistFromPrev);
        Assert.True(points[1].DistFromPrev > 0);
        Assert.True(points[1].CalcSpeed > 0);
    }

    [Fact]
    public void ClampSpeeds_ClampsExcessiveSpeed()
    {
        var points = new List<TrackPoint>
        {
            new() { CalcSpeed = 5.0 },
            new() { CalcSpeed = 100.0 },
            new() { CalcSpeed = 5.0 },
        };
        SpeedCalculator.ClampSpeeds(points, 25.0);
        Assert.Equal(5.0, points[0].CalcSpeed);
        Assert.Equal(0.0, points[1].CalcSpeed);
        Assert.Equal(5.0, points[2].CalcSpeed);
    }

    [Fact]
    public void PresetMaxSpeed_ContainsAllPresets()
    {
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("hiking"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("trail"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("cycling"));
    }
}
