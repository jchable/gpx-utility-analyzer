using System.Diagnostics;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Benchmark;

public sealed class RunConfig
{
    public required List<TrackPoint> Points { get; init; }
    public int SegmentCount { get; init; }
    public IElevationProvider? DemSource { get; init; }
    public int MaxHR { get; init; }
    public bool Verbose { get; init; }
}

public static class BenchmarkRunner
{
    public static List<RunResult> Run(List<BenchmarkCombination> combos, RunConfig cfg)
    {
        var results = new List<RunResult>(combos.Count);

        for (int i = 0; i < combos.Count; i++)
        {
            var combo = combos[i];
            if (cfg.Verbose)
                Console.Error.Write($"\r  [{i + 1}/{combos.Count}] {combo.Label()}");

            // Copy the points so one combination's in-place mutations cannot reach the
            // next one. Clone() is a memberwise copy: a hand-listed projection silently
            // drops whatever field is added to TrackPoint next, and dropping
            // StartsNewSegment made `benchmark` flatten the recording boundaries that
            // `analyze` honours.
            var pointsCopy = cfg.Points.Select(p => p.Clone()).ToList();

            var computeCfg = BuildComputeConfig(combo, cfg.DemSource, cfg.MaxHR);
            var sw = Stopwatch.StartNew();
            var (summary, _) = ComputePipeline.Compute(pointsCopy, cfg.SegmentCount, computeCfg);
            sw.Stop();

            results.Add(new RunResult
            {
                Combination = combo,
                Distance2D = summary.TotalDistance / 1000,
                Distance3D = summary.TotalDistance3D / 1000,
                ElevGain = summary.Elevation.Gain,
                ElevLoss = summary.Elevation.Loss,
                ElevMax = summary.Elevation.Max,
                ElevMin = summary.Elevation.Min,
                MovingTime = summary.MovingTime,
                StoppedTime = summary.StoppedTime,
                StopCount = summary.StopCount,
                AvgSpeed = summary.Speed.AvgSpeed * 3.6,
                MaxSpeed = summary.Speed.MaxSpeed * 3.6,
                FilteredPoints = summary.FilteredPoints,
                ComputeDuration = sw.Elapsed
            });
        }

        if (cfg.Verbose)
            Console.Error.WriteLine();

        return results;
    }

    private static ComputeConfig BuildComputeConfig(
        BenchmarkCombination combo, IElevationProvider? demSrc, int maxHR)
    {
        var stopCfg = StopDetector.Presets.GetValueOrDefault(combo.Preset,
            StopDetector.Presets[StopDetector.PresetHiking]);

        double maxSpeed = SpeedCalculator.PresetMaxSpeed.GetValueOrDefault(combo.Preset,
            SpeedCalculator.DefaultMaxReasonableSpeed);

        return new ComputeConfig
        {
            ElevationThreshold = combo.Threshold,
            StopConfig = stopCfg,
            SmoothingLevel = combo.ElevSmoothing,
            DemSource = combo.UseDem ? demSrc : null,
            ElevationCfg = new ElevationConfig
            {
                Algo = combo.ElevAlgo,
                Threshold = combo.Threshold,
                Epsilon = combo.DpEpsilon,
                MinSegLen = combo.SegMinLen,
                MaxSlopeDev = combo.SegMaxDev,
            },
            TrackSmoothing = combo.TrackSmoothing,
            BiometricsCfg = new BiometricsConfig { MaxHR = maxHR },
            MaxReasonableSpeed = maxSpeed,
        };
    }
}
