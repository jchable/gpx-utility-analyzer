using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Dem;

namespace GpxAnalyzer.Cli.Core.Stats;

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

    /// <summary>If non-null, anomaly detection is enabled with these thresholds.</summary>
    public AnomalyConfig? AnomalyConfig { get; init; }

    /// <summary>If true (and AnomalyConfig is set), apply automatic anomaly corrections.</summary>
    public bool FixAnomalies { get; init; }

    public static ComputeConfig Default() => new();
}
