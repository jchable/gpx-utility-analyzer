using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

internal static class SharedFlags
{
    public static ComputeConfig BuildConfigFromContext(InvocationContext ctx,
        Option<string> presetOpt, Option<double> stopSpeedOpt, Option<double> stopDurationOpt,
        Option<double> elevThresholdOpt, Option<string> smoothingOpt,
        Option<string> demDirOpt, Option<string> demCacheOpt, Option<bool> demAutoOpt,
        Option<int> demMaxMemOpt, Option<bool> demSkipValOpt,
        Option<string> elevAlgoOpt, Option<string> trackSmoothOpt,
        Option<double> dpEpsOpt, Option<double> segMinLenOpt, Option<double> segMaxDevOpt,
        Option<int> maxHrOpt, Option<double> maxSpeedOpt, Option<bool>? fixAnomaliesOpt = null)
    {
        return BuildConfig(
            ctx.ParseResult.GetValueForOption(presetOpt) ?? "hiking",
            ctx.ParseResult.GetValueForOption(stopSpeedOpt),
            ctx.ParseResult.GetValueForOption(stopDurationOpt),
            ctx.ParseResult.GetValueForOption(elevThresholdOpt),
            ctx.ParseResult.GetValueForOption(smoothingOpt) ?? "medium",
            ctx.ParseResult.GetValueForOption(demDirOpt) ?? "",
            ctx.ParseResult.GetValueForOption(demCacheOpt) ?? "",
            ctx.ParseResult.GetValueForOption(demAutoOpt),
            ctx.ParseResult.GetValueForOption(demMaxMemOpt),
            ctx.ParseResult.GetValueForOption(demSkipValOpt),
            ctx.ParseResult.GetValueForOption(elevAlgoOpt) ?? "threshold",
            ctx.ParseResult.GetValueForOption(trackSmoothOpt) ?? "none",
            ctx.ParseResult.GetValueForOption(dpEpsOpt),
            ctx.ParseResult.GetValueForOption(segMinLenOpt),
            ctx.ParseResult.GetValueForOption(segMaxDevOpt),
            ctx.ParseResult.GetValueForOption(maxHrOpt),
            ctx.ParseResult.GetValueForOption(maxSpeedOpt),
            fixAnomaliesOpt != null && ctx.ParseResult.GetValueForOption(fixAnomaliesOpt));
    }

    public static ComputeConfig BuildConfig(string preset, double stopSpeed, double stopDuration,
        double elevThreshold, string smoothing, string demDir, string demCache,
        bool demAuto, int demMaxMem, bool demSkipVal, string elevAlgo,
        string trackSmooth, double dpEps, double segMinLen, double segMaxDev,
        int maxHr, double maxSpeed, bool fixAnomalies = false)
    {
        if (!StopDetector.Presets.TryGetValue(preset, out var stopCfg))
        {
            Console.Error.WriteLine($"Warning: unknown preset '{preset}', using hiking");
            stopCfg = StopDetector.Presets[StopDetector.PresetHiking];
        }

        if (stopSpeed > 0)
            stopCfg = new StopConfig { MaxSpeed = stopSpeed, MinDuration = stopCfg.MinDuration, MaxDistance = stopCfg.MaxDistance };
        if (stopDuration > 0)
            stopCfg = new StopConfig { MaxSpeed = stopCfg.MaxSpeed, MinDuration = TimeSpan.FromSeconds(stopDuration), MaxDistance = stopCfg.MaxDistance };

        if (!ElevationSmoother.IsValidLevel(smoothing))
        {
            Console.Error.WriteLine($"Warning: unknown smoothing '{smoothing}', using medium");
            smoothing = "medium";
        }

        IElevationProvider? demSource = null;
        string cacheDir = string.IsNullOrEmpty(demCache) ? DemSource.DefaultCacheDir() : demCache;
        if (!string.IsNullOrEmpty(demDir) && demAuto)
            demSource = DemSource.CreateWithCache(demDir, cacheDir, true).WithMaxMemory(demMaxMem).WithSkipValidation(demSkipVal);
        else if (!string.IsNullOrEmpty(demDir))
            demSource = DemSource.CreateWithCache(demDir, cacheDir, false).WithMaxMemory(demMaxMem).WithSkipValidation(demSkipVal);
        else if (demAuto)
            demSource = DemSource.CreateAuto(cacheDir).WithMaxMemory(demMaxMem).WithSkipValidation(demSkipVal);

        if (!ElevationAlgo.IsValid(elevAlgo))
        {
            Console.Error.WriteLine($"Warning: unknown elevation algo '{elevAlgo}', using threshold");
            elevAlgo = ElevationAlgo.Threshold;
        }

        if (!TrackSmoother.IsValidLevel(trackSmooth))
        {
            Console.Error.WriteLine($"Warning: unknown track smoothing '{trackSmooth}', using none");
            trackSmooth = "none";
        }

        double maxReasonable = maxSpeed;
        if (maxReasonable <= 0 && SpeedCalculator.PresetMaxSpeed.TryGetValue(preset, out var presetMax))
            maxReasonable = presetMax;

        return new ComputeConfig
        {
            ElevationThreshold = elevThreshold,
            StopConfig = stopCfg,
            SmoothingLevel = smoothing,
            DemSource = demSource,
            ElevationCfg = new ElevationConfig
            {
                Algo = elevAlgo,
                Threshold = elevThreshold,
                Epsilon = dpEps,
                MinSegLen = segMinLen,
                MaxSlopeDev = segMaxDev,
            },
            TrackSmoothing = trackSmooth,
            BiometricsCfg = new BiometricsConfig { MaxHR = maxHr },
            MaxReasonableSpeed = maxReasonable,
            AnomalyConfig = AnomalyConfig.Default(),
            FixAnomalies = fixAnomalies,
        };
    }
}
