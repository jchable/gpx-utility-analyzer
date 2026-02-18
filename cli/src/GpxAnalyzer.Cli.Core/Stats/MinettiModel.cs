namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Minetti et al. (2002) metabolic cost model for graded walking/running.
/// Extracted for reuse by both ProfileComputationService (API) and EffortCalculator (Core).
/// </summary>
public static class MinettiModel
{
    /// <summary>Cost at zero grade (flat) in J/kg/m.</summary>
    public static readonly double CostFlat = Cost(0);

    /// <summary>
    /// Metabolic cost C(i) in J/kg/m, where i is grade as a fraction (e.g. 0.10 = 10%).
    /// Polynomial from Minetti et al. (2002).
    /// </summary>
    public static double Cost(double grade)
    {
        var g = Math.Clamp(grade, -0.45, 0.45);
        var cost = 155.4 * Math.Pow(g, 5)
                 - 30.4 * Math.Pow(g, 4)
                 - 43.3 * Math.Pow(g, 3)
                 + 46.3 * Math.Pow(g, 2)
                 + 19.5 * g
                 + 3.6;
        return Math.Max(cost, 0.1);
    }
}
