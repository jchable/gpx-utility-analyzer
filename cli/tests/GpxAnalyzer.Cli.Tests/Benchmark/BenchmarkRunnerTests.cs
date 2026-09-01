using GpxAnalyzer.Cli.Core.Benchmark;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Benchmark;

/// <summary>
/// `benchmark` must report, for a given combination, exactly what `analyze` reports for the
/// equivalent configuration. It runs the very same <see cref="ComputePipeline"/>, so the only
/// way the two can disagree is the per-combination copy of the points made in
/// <see cref="BenchmarkRunner"/>: whatever that copy drops, the pipeline never sees.
/// </summary>
public class BenchmarkRunnerTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Two recording segments a single minute apart - close enough that the time-gap
    /// heuristic in <see cref="SpeedCalculator.EnrichPoints"/> cannot rediscover the
    /// boundary, so only <see cref="TrackPoint.StartsNewSegment"/> carries it.
    ///
    /// Each segment climbs 20 m over two minutes; the 30 m step across the boundary is not
    /// climbed at all. Honouring the boundary therefore gives +40 m and 4 minutes of
    /// recorded time; flattening it gives +70 m and 5 minutes.
    /// </summary>
    private static List<TrackPoint> TwoSegmentTrack()
    {
        var points = new List<TrackPoint>();
        double[] elevations = [100, 110, 120, 150, 160, 170];
        for (int i = 0; i < elevations.Length; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 45.0 + i * 0.001,   // ~111 m per minute: 1.85 m/s, well under every preset cap
                Lon = 6.0,
                Ele = elevations[i],
                Time = T0.AddMinutes(i),
                StartsNewSegment = i == 3,
            });
        }
        return points;
    }

    /// <summary>Mirrors BenchmarkRunner's own private BuildComputeConfig for this combination.</summary>
    private static ComputeConfig EquivalentConfig(BenchmarkCombination combo) => new()
    {
        ElevationThreshold = combo.Threshold,
        StopConfig = StopDetector.Presets[combo.Preset],
        SmoothingLevel = combo.ElevSmoothing,
        DemSource = null,
        ElevationCfg = new ElevationConfig
        {
            Algo = combo.ElevAlgo,
            Threshold = combo.Threshold,
            Epsilon = combo.DpEpsilon,
            MinSegLen = combo.SegMinLen,
            MaxSlopeDev = combo.SegMaxDev,
        },
        TrackSmoothing = combo.TrackSmoothing,
        BiometricsCfg = new BiometricsConfig { MaxHR = 0 },
        MaxReasonableSpeed = SpeedCalculator.PresetMaxSpeed[combo.Preset],
    };

    private static BenchmarkCombination NoSmoothingNoDem() =>
        new() { ElevSmoothing = "none", UseDem = false };

    [Fact]
    public void Run_TrackWithASegmentBoundary_HonoursIt()
    {
        var combo = NoSmoothingNoDem();

        var results = BenchmarkRunner.Run([combo], new RunConfig
        {
            Points = TwoSegmentTrack(),
            SegmentCount = 2,
        });

        // The 30 m step across the boundary is not a climb, and the minute across it was
        // not recorded by either segment.
        Assert.Equal(40, results[0].ElevGain, 6);
        Assert.Equal(TimeSpan.FromMinutes(4), results[0].MovingTime);
    }

    [Fact]
    public void Run_TrackWithASegmentBoundary_AgreesWithComputePipeline()
    {
        var combo = NoSmoothingNoDem();

        var results = BenchmarkRunner.Run([combo], new RunConfig
        {
            Points = TwoSegmentTrack(),
            SegmentCount = 2,
        });
        var (expected, _) = ComputePipeline.Compute(TwoSegmentTrack(), 2, EquivalentConfig(combo));

        Assert.Equal(expected.Elevation.Gain, results[0].ElevGain, 6);
        Assert.Equal(expected.Elevation.Loss, results[0].ElevLoss, 6);
        Assert.Equal(expected.MovingTime, results[0].MovingTime);
        Assert.Equal(expected.StoppedTime, results[0].StoppedTime);
        Assert.Equal(expected.TotalDistance / 1000, results[0].Distance2D, 9);
    }

    /// <summary>
    /// The copy must not let one combination's in-place mutations reach the next one, and
    /// must not let them reach the caller's list either.
    /// </summary>
    [Fact]
    public void Run_SeveralCombinations_LeavesTheCallersPointsUntouched()
    {
        var points = TwoSegmentTrack();
        var before = points.Select(p => (p.Lat, p.Lon, p.Ele, p.StartsNewSegment)).ToList();

        BenchmarkRunner.Run(
            [new BenchmarkCombination { ElevSmoothing = "heavy", TrackSmoothing = "heavy", UseDem = false },
             NoSmoothingNoDem()],
            new RunConfig { Points = points, SegmentCount = 2 });

        Assert.Equal(before, points.Select(p => (p.Lat, p.Lon, p.Ele, p.StartsNewSegment)).ToList());
    }

}
