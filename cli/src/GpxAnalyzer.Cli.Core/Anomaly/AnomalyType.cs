namespace GpxAnalyzer.Cli.Core.Anomaly;

public enum AnomalyType
{
    // Position
    GpsFrozen,
    GpsTeleportation,
    GpsDrift,
    SignalLoss,

    // Speed
    SpeedSpike,
    SpeedBiometricMismatch,

    // Elevation
    ElevationSpike,
    ImpossibleGrade,

    // Temporal
    BackwardTime,
    DuplicateTimestamp,

    // Biometric
    HeartRateSpike,
    HeartRateOutOfRange,

    // Data Quality
    LowPointDensity,
    ConstantElevation
}
