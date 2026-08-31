using GpxAnalyzer.Cli.Commands;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Commands;

public class SharedFlagsTests
{
    private static ComputeConfig Build(string preset) => SharedFlags.BuildConfig(
        preset: preset, stopSpeed: 0, stopDuration: 0, elevThreshold: 2.0,
        smoothing: "medium", demDir: "", demCache: "", demAuto: false,
        demMaxMem: 0, demSkipVal: false, elevAlgo: "threshold",
        trackSmooth: "none", dpEps: 3.0, segMinLen: 200.0, segMaxDev: 2.0,
        maxHr: 0, maxSpeed: 0);

    [Theory]
    [InlineData("Trail")]     // right preset, wrong case - lookups are ordinal
    [InlineData("hikking")]   // typo
    [InlineData("nonsense")]
    public void BuildConfig_UnknownPreset_StillEnablesGpsOutlierFiltering(string preset)
    {
        var cfg = Build(preset);

        // MaxReasonableSpeed = 0 turns off FilterOutliers, ClampSpeeds AND
        // DetectSpeedSpikes - an unknown preset must not do that silently.
        Assert.True(cfg.MaxReasonableSpeed > 0,
            $"preset '{preset}' fell back to hiking for stops but left MaxReasonableSpeed at {cfg.MaxReasonableSpeed}");
        Assert.Equal(
            SpeedCalculator.PresetMaxSpeed[StopDetector.PresetHiking],
            cfg.MaxReasonableSpeed);
    }

    [Fact]
    public void BuildConfig_KnownPreset_UsesItsOwnThreshold()
    {
        Assert.Equal(SpeedCalculator.PresetMaxSpeed[StopDetector.PresetTrail],
            Build(StopDetector.PresetTrail).MaxReasonableSpeed);
        Assert.Equal(SpeedCalculator.PresetMaxSpeed[StopDetector.PresetCycling],
            Build(StopDetector.PresetCycling).MaxReasonableSpeed);
    }
}
