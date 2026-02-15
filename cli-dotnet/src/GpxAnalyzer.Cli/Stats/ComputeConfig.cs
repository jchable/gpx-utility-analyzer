using GpxAnalyzer.Cli.Dem;

namespace GpxAnalyzer.Cli.Stats;

/// <summary>
/// Configuration for the compute pipeline.
/// </summary>
public sealed class ComputeConfig
{
    public double ElevationThreshold { get; init; } = 2.0;
    public StopConfig StopConfig { get; init; } = StopDetector.Presets[StopDetector.PresetHiking];
    public string SmoothingLevel { get; init; } = "medium";
    public IElevationProvider? DemSource { get; init; }
    public ElevationConfig ElevationCfg { get; init; } = new();
    public string TrackSmoothing { get; init; } = "none";
    public BiometricsConfig BiometricsCfg { get; init; } = new();
    public double MaxReasonableSpeed { get; init; }

    public static ComputeConfig Default() => new();
}
