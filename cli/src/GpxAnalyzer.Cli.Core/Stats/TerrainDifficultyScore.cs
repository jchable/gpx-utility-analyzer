namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Multi-criteria terrain difficulty assessment.
/// </summary>
public sealed class TerrainDifficultyScore
{
    /// <summary>Composite score from 1 (easy) to 10 (extreme).</summary>
    public double Score { get; init; }

    /// <summary>Human-readable grade: Easy, Moderate, Hard, Expert, Extreme.</summary>
    public string Grade { get; init; } = "";

    /// <summary>Average absolute grade in percent.</summary>
    public double AvgGradePercent { get; init; }

    /// <summary>Maximum absolute grade in percent.</summary>
    public double MaxGradePercent { get; init; }

    /// <summary>Variance of grade values — measures terrain irregularity.</summary>
    public double GradeVariance { get; init; }

    /// <summary>Fraction of track distance where absolute grade exceeds 15%.</summary>
    public double SteepSectionRatio { get; init; }

    /// <summary>Elevation gain per horizontal kilometer (D+ / km).</summary>
    public double ElevationPerKm { get; init; }
}
