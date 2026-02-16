using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class ElevationCalculatorTests
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
    public void ComputeThreshold_FlatTrack_ZeroGainLoss()
    {
        var points = MakePoints(100, 100, 100, 100);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.Threshold, Threshold = 2.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.Equal(0, result.Gain);
        Assert.Equal(0, result.Loss);
    }

    [Fact]
    public void ComputeThreshold_SteadyClimb_CorrectGain()
    {
        // 100, 103, 106, 109 → gain = 9 (each step > 2m threshold)
        var points = MakePoints(100, 103, 106, 109);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.Threshold, Threshold = 2.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.Equal(9, result.Gain, 0.1);
        Assert.Equal(0, result.Loss, 0.1);
    }

    [Fact]
    public void ComputeThreshold_SteadyDescent_CorrectLoss()
    {
        var points = MakePoints(109, 106, 103, 100);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.Threshold, Threshold = 2.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.Equal(0, result.Gain, 0.1);
        Assert.Equal(9, result.Loss, 0.1);
    }

    [Fact]
    public void ComputeThreshold_SmallVariation_Filtered()
    {
        // Changes of 1m are below 2m threshold
        var points = MakePoints(100, 101, 100, 101, 100);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.Threshold, Threshold = 2.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.Equal(0, result.Gain, 0.1);
        Assert.Equal(0, result.Loss, 0.1);
    }

    [Fact]
    public void ComputeThreshold_MinMax()
    {
        var points = MakePoints(100, 150, 120, 200);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.Threshold, Threshold = 2.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.Equal(200, result.Max, 0.1);
        Assert.Equal(100, result.Min, 0.1);
    }

    [Fact]
    public void ComputeDouglasPeucker_ReturnsResult()
    {
        var points = MakePoints(100, 110, 105, 120, 130);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.DouglasPeucker, Epsilon = 3.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.True(result.Gain >= 0);
        Assert.True(result.Loss >= 0);
    }

    [Fact]
    public void ComputeSegments_ReturnsResult()
    {
        var points = MakePoints(100, 105, 110, 115, 120, 115, 110, 105, 100);
        var cfg = new ElevationConfig { Algo = ElevationAlgo.Segments, MinSegLen = 100, MaxSlopeDev = 2.0 };
        var result = ElevationCalculator.ComputeWithAlgo(points, cfg);
        Assert.True(result.Gain >= 0);
        Assert.True(result.Loss >= 0);
    }
}
