namespace GpxAnalyzer.Cli.Core.Anomaly;

/// <summary>
/// Aggregated anomaly report for a track.
/// </summary>
public sealed class AnomalyReport
{
    public List<TrackAnomaly> Anomalies { get; init; } = [];

    public int InfoCount => Anomalies.Count(a => a.Severity == AnomalySeverity.Info);
    public int WarningCount => Anomalies.Count(a => a.Severity == AnomalySeverity.Warning);
    public int CriticalCount => Anomalies.Count(a => a.Severity == AnomalySeverity.Critical);
    public int TotalCount => Anomalies.Count;

    /// <summary>Overall quality score (0-100, 100 = perfect).</summary>
    public int QualityScore { get; init; }

    /// <summary>Total estimated distance impact in meters.</summary>
    public double TotalDistanceImpactM { get; init; }

    /// <summary>Total estimated time impact in seconds.</summary>
    public double TotalTimeImpactS { get; init; }

    /// <summary>Whether corrections were applied.</summary>
    public bool CorrectionApplied { get; init; }

    /// <summary>True if no anomalies detected.</summary>
    public bool IsClean => Anomalies.Count == 0;

    /// <summary>Compute quality score from anomaly list.</summary>
    public static int ComputeQualityScore(List<TrackAnomaly> anomalies)
    {
        double score = 100;
        foreach (var a in anomalies)
        {
            score -= a.Severity switch
            {
                AnomalySeverity.Critical => 15,
                AnomalySeverity.Warning => 5,
                AnomalySeverity.Info => 1,
                _ => 0
            };
        }
        return Math.Max(0, (int)Math.Round(score));
    }
}
