namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Aggregated effort metrics computed from track data.
/// </summary>
public sealed class EffortMetrics
{
    // Feature 1: Time estimates
    public TimeSpan NaismithTime { get; init; }
    public TimeSpan ToblerTime { get; init; }
    public TimeSpan MunterTime { get; init; }

    /// <summary>Actual moving time / Naismith estimate (1.0 = matching, &lt;1 = faster).</summary>
    public double PerformanceRatioNaismith { get; init; }

    /// <summary>Actual moving time / Tobler estimate (1.0 = matching, &lt;1 = faster).</summary>
    public double PerformanceRatioTobler { get; init; }

    // Feature 2: Kilomètre-effort (CAS/SAC)
    public double KilometreEffort { get; init; }

    // Feature 3: ITRA points
    public double ItraPoints { get; init; }
    public string ItraCategory { get; init; } = "";

    // Feature 5: Equivalent flat distance (Minetti)
    public double EquivalentFlatDistanceKm { get; init; }

    // Feature 9: Terrain difficulty
    public TerrainDifficultyScore TerrainDifficulty { get; init; } = new();
}
