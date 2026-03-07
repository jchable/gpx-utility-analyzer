namespace GpxAnalyzer.Cli.Core.Anomaly;

/// <summary>
/// A single detected anomaly in the track data.
/// </summary>
public sealed class TrackAnomaly
{
    public AnomalyType Type { get; init; }
    public AnomalySeverity Severity { get; init; }
    public AnomalyCategory Category { get; init; }

    /// <summary>Index of first affected point in the processed point list.</summary>
    public int StartIndex { get; init; }

    /// <summary>Index of last affected point (inclusive).</summary>
    public int EndIndex { get; init; }

    /// <summary>Start time of the anomalous section.</summary>
    public DateTime? StartTime { get; init; }

    /// <summary>End time of the anomalous section.</summary>
    public DateTime? EndTime { get; init; }

    /// <summary>Estimated distance impact in meters (positive = inflated, negative = lost).</summary>
    public double DistanceImpactM { get; init; }

    /// <summary>Estimated time impact in seconds.</summary>
    public double TimeImpactS { get; init; }

    /// <summary>Human-readable description of the anomaly.</summary>
    public string Description { get; init; } = "";

    /// <summary>Whether this anomaly was corrected (only true when --fix-anomalies is used).</summary>
    public bool WasCorrected { get; init; }
}
