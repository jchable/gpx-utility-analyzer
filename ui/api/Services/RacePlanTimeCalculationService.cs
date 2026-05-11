namespace GpxAnalyzer.Api.Services;

using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Calcule les temps de passage aux checkpoints d'un plan de course
/// à partir de la formule de Tobler et d'un coefficient de performance personnel.
///
/// Convention du PerformanceCoefficient (ratio de vitesse) :
///   coeff = 1.0 → même vitesse que Tobler (théorique)
///   coeff = 0.75 → 75% de la vitesse Tobler (plus lent, temps × 1/0.75)
///   coeff = 1.2 → 20% plus rapide que Tobler
/// </summary>
public class RacePlanTimeCalculationService
{
    /// <summary>
    /// Recalcule TargetArrivalSeconds pour tous les checkpoints d'un plan.
    /// Les checkpoints sont triés par DistanceKm. Le premier (start) reçoit 0.
    /// </summary>
    public static void ComputeCheckpointTimes(
        double[][] allPoints,
        IList<RacePlanCheckpoint> checkpoints,
        double performanceCoefficient)
    {
        if (allPoints.Length < 2 || checkpoints.Count == 0) return;

        // Garde-fou pour éviter la division par zéro ou les valeurs absurdes
        if (performanceCoefficient < 0.1) performanceCoefficient = 0.1;
        if (performanceCoefficient > 5.0) performanceCoefficient = 5.0;

        // Calcul des distances cumulées pour chaque point brut (en km)
        var cumDist = ComputeCumulativeDistances(allPoints);

        // Tri des checkpoints par distance
        var sorted = checkpoints.OrderBy(c => c.DistanceKm).ToList();

        // Le premier checkpoint (départ) = T+0
        sorted[0].TargetArrivalSeconds = 0;

        for (int i = 1; i < sorted.Count; i++)
        {
            int prevIdx = FindClosestIndex(cumDist, sorted[i - 1].DistanceKm);
            int currIdx = FindClosestIndex(cumDist, sorted[i].DistanceKm);

            double toblerSeconds = ComputeToblerSeconds(allPoints, prevIdx, currIdx);
            double adjustedSeconds = toblerSeconds / performanceCoefficient;

            int prevPause = sorted[i - 1].PlannedPauseSeconds ?? 0;
            int prevArrival = sorted[i - 1].TargetArrivalSeconds ?? 0;

            sorted[i].TargetArrivalSeconds = prevArrival + prevPause + (int)Math.Round(adjustedSeconds);
        }
    }

    /// <summary>
    /// Calcule la durée Tobler (secondes) pour un segment entre deux indices de points.
    /// </summary>
    public static double ComputeToblerSeconds(double[][] coords, int startIdx, int endIdx)
    {
        endIdx = Math.Min(endIdx, coords.Length - 1);
        double totalSeconds = 0;

        for (int i = startIdx + 1; i <= endIdx; i++)
        {
            double distM = DistanceCalculator.Haversine(
                coords[i - 1][1], coords[i - 1][0],
                coords[i][1], coords[i][0]);

            if (distM < 0.01) continue;

            double prevEle = coords[i - 1].Length > 2 ? coords[i - 1][2] : 0;
            double ele = coords[i].Length > 2 ? coords[i][2] : 0;
            double grade = (ele - prevEle) / distM; // fraction (pas pourcentage)

            double speedKmh = ToblerSpeed(grade);
            double speedMs = speedKmh / 3.6;

            if (speedMs > 0.01)
                totalSeconds += distM / speedMs;
        }

        return totalSeconds;
    }

    /// <summary>
    /// Tobler's hiking function: V(s) = 6 × exp(-3.5 × |s + 0.05|) km/h.
    /// grade = élévation / distance (fraction, ex: 0.10 pour 10%)
    /// </summary>
    public static double ToblerSpeed(double grade)
        => 6.0 * Math.Exp(-3.5 * Math.Abs(grade + 0.05));

    /// <summary>
    /// Calcule la durée Tobler totale d'une trace (tous les points).
    /// Retourne les secondes.
    /// </summary>
    public static double ComputeTotalToblerSeconds(double[][] coords)
        => ComputeToblerSeconds(coords, 0, coords.Length - 1);

    /// <summary>
    /// Calcule les distances cumulées depuis le premier point (en km).
    /// </summary>
    public static double[] ComputeCumulativeDistances(double[][] coords)
    {
        var cumDist = new double[coords.Length];
        for (int i = 1; i < coords.Length; i++)
        {
            cumDist[i] = cumDist[i - 1] + DistanceCalculator.Haversine(
                coords[i - 1][1], coords[i - 1][0],
                coords[i][1], coords[i][0]) / 1000.0;
        }
        return cumDist;
    }

    /// <summary>
    /// Trouve l'index du point le plus proche d'une distance cumulée donnée (km).
    /// </summary>
    private static int FindClosestIndex(double[] cumDist, double targetKm)
    {
        int best = 0;
        double bestDiff = Math.Abs(cumDist[0] - targetKm);

        for (int i = 1; i < cumDist.Length; i++)
        {
            double diff = Math.Abs(cumDist[i] - targetKm);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = i;
            }
            // Optimisation : on arrête si on dépasse la cible de plus de 0.5 km
            if (cumDist[i] > targetKm + 0.5) break;
        }

        return best;
    }
}
