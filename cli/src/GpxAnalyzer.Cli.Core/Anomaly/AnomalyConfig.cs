namespace GpxAnalyzer.Cli.Core.Anomaly;

/// <summary>
/// Configurable thresholds for anomaly detection.
/// </summary>
public sealed class AnomalyConfig
{
    // Position
    /// <summary>Minimum consecutive identical-position points to flag GPS frozen.</summary>
    public int GpsFrozenMinPoints { get; init; } = 5;

    /// <summary>Coordinate epsilon for "identical" position (decimal degrees, ~0.1m).</summary>
    public double GpsFrozenEpsilon { get; init; } = 0.000001;

    /// <summary>Max time gap between consecutive points before flagging signal loss (seconds).</summary>
    public double SignalLossThresholdS { get; init; } = 30;

    /// <summary>Max position drift (meters) during stops before flagging GPS drift.</summary>
    public double GpsDriftThresholdM { get; init; } = 20;

    // Elevation
    /// <summary>Max single-point elevation change before flagging spike (meters).</summary>
    public double ElevationSpikeThresholdM { get; init; } = 50;

    /// <summary>Max grade percentage before flagging impossible grade.</summary>
    public double ImpossibleGradePercent { get; init; } = 80;

    // Biometrics
    /// <summary>Max HR change between adjacent points (bpm).</summary>
    public int HrSpikeThresholdBpm { get; init; } = 30;

    /// <summary>Minimum plausible HR value.</summary>
    public int HrMinBpm { get; init; } = 30;

    /// <summary>Maximum plausible HR value.</summary>
    public int HrMaxBpm { get; init; } = 230;

    // Data Quality
    /// <summary>Minimum points per km for acceptable density.</summary>
    public double MinPointsPerKm { get; init; } = 5;

    /// <summary>Minimum elevation range to rule out barometer failure (meters).</summary>
    public double ConstantElevationRangeM { get; init; } = 2;

    /// <summary>Minimum points to evaluate constant elevation.</summary>
    public int ConstantElevationMinPoints { get; init; } = 100;

    // Speed/Biometric consistency
    /// <summary>Minimum cadence (rpm) to consider "active movement".</summary>
    public int ActiveCadenceThreshold { get; init; } = 30;

    public static AnomalyConfig Default() => new();
}
