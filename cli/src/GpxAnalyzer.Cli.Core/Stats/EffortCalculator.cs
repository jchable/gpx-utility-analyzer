using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Computes effort-related metrics: time estimates (Naismith, Tobler, Munter),
/// kilomètre-effort, ITRA points, equivalent flat distance, and terrain difficulty.
/// </summary>
public static class EffortCalculator
{
    /// <summary>
    /// Shortest segment whose grade is meaningful. Below a few metres the
    /// denominator is GPS jitter and the numerator independent elevation noise,
    /// so the quotient is arbitrary — and it propagated straight into
    /// MaxGradePercent and the composite difficulty score.
    ///
    /// Applies to ComputeTerrainDifficulty ONLY. Tobler and Minetti bound a wild
    /// grade themselves (exp decay + a min-speed guard; a clamp to +/-0.45), and
    /// they consume the segment DISTANCE, so a floor there would not sanitise a
    /// quotient — it would silently drop real distance. At 1 Hz, the default
    /// cadence on most watches, that is every segment of a running track.
    /// </summary>
    private const double MinGradeSegmentM = 5.0;

    // ── Feature 1: Time estimates ──

    /// <summary>
    /// Naismith's rule (1892): 5 km/h + 1 hour per 600 m of ascent.
    /// </summary>
    public static TimeSpan NaismithTime(double distanceM, double elevGainM)
    {
        var hours = (distanceM / 1000.0) / 5.0 + elevGainM / 600.0;
        return TimeSpan.FromHours(Math.Max(hours, 0));
    }

    /// <summary>
    /// Tobler's hiking function (1993): V(s) = 6 × exp(-3.5 × |s + 0.05|) km/h.
    /// Per-segment calculation for precision.
    /// </summary>
    public static TimeSpan ToblerTime(List<TrackPoint> points)
    {
        if (points.Count < 2)
            return TimeSpan.Zero;

        double totalSeconds = 0;
        for (int i = 1; i < points.Count; i++)
        {
            var dist = points[i].DistFromPrev;
            // 0.1 m, not MinGradeSegmentM: Tobler saturates (exp decay plus the
            // speedMs > 0.01 guard below), and a 5 m floor would discard EVERY
            // segment of a 1 Hz recording - the default cadence on most watches.
            if (dist < 0.1) continue; // skip negligible segments

            var dEle = points[i].Ele - points[i - 1].Ele;
            var grade = dEle / dist; // rise/run
            var speedKmh = ToblerSpeed(grade);
            var speedMs = speedKmh / 3.6;

            if (speedMs > 0.01) // guard against near-zero speeds on extreme grades
                totalSeconds += dist / speedMs;
        }

        // Guard against overflow for extreme tracks
        if (totalSeconds > 3_155_760_000) // ~100 years
            totalSeconds = 3_155_760_000;

        return TimeSpan.FromSeconds(totalSeconds);
    }

    /// <summary>
    /// Tobler walking speed in km/h for a given grade (fraction, not percent).
    /// </summary>
    public static double ToblerSpeed(double grade)
    {
        return 6.0 * Math.Exp(-3.5 * Math.Abs(grade + 0.05));
    }

    /// <summary>
    /// Munter estimation (Swiss Alpine Club CAS/SAC).
    /// Horizontal: 4 km/h, Ascent: 400 m/h, Descent: 800 m/h.
    /// Formula: max(horiz, ascent) + min(horiz, ascent)/2, plus descent contribution.
    /// </summary>
    public static TimeSpan MunterTime(double distanceM, double elevGainM, double elevLossM)
    {
        var horizHours = (distanceM / 1000.0) / 4.0;
        var ascentHours = elevGainM / 400.0;
        var descentHours = elevLossM / 800.0;

        // Ascent component: max + min/2 rule
        var ascentComponent = Math.Max(horizHours, ascentHours) + Math.Min(horizHours, ascentHours) / 2.0;

        // Add descent contribution if significant
        var hours = ascentComponent + descentHours;

        return TimeSpan.FromHours(Math.Max(hours, 0));
    }

    // ── Feature 2: Kilomètre-effort ──

    /// <summary>
    /// CAS/SAC convention: KE = distance_km + D+/100 + D-/150.
    /// </summary>
    public static double KilometreEffort(double distanceKm, double elevGainM, double elevLossM)
    {
        return distanceKm + elevGainM / 100.0 + elevLossM / 150.0;
    }

    // ── Feature 3: ITRA points ──

    /// <summary>
    /// ITRA points = distance_km + D+/100.
    /// </summary>
    public static double ItraPoints(double distanceKm, double elevGainM)
    {
        return distanceKm + elevGainM / 100.0;
    }

    /// <summary>
    /// ITRA category from points: XXS, XS, S, M, L, XL, XXL.
    /// </summary>
    public static string ItraCategory(double points) => points switch
    {
        < 25 => "XXS",
        < 45 => "XS",
        < 65 => "S",
        < 90 => "M",
        < 120 => "L",
        < 160 => "XL",
        _ => "XXL",
    };

    // ── Feature 5: Equivalent flat distance (Minetti) ──

    /// <summary>
    /// Equivalent flat distance using Minetti metabolic cost model.
    /// EFD = Σ(segment_dist × MinettiCost(grade) / MinettiCost(0)).
    /// </summary>
    public static double EquivalentFlatDistance(List<TrackPoint> points)
    {
        if (points.Count < 2)
            return 0;

        double efd = 0;
        for (int i = 1; i < points.Count; i++)
        {
            var dist = points[i].DistFromPrev;
            // 0.1 m, not MinGradeSegmentM: Minetti clamps the grade to +/-0.45, so a
            // jitter segment contributes at most its own (tiny) length x 5.4, and a
            // 5 m floor would zero the metric outright for 1 Hz recordings.
            if (dist < 0.1) continue;

            var dEle = points[i].Ele - points[i - 1].Ele;
            var grade = dEle / dist;
            var costRatio = MinettiModel.Cost(grade) / MinettiModel.CostFlat;
            efd += dist * costRatio;
        }

        return efd;
    }

    // ── Feature 9: Terrain difficulty score ──

    /// <summary>
    /// Multi-criteria terrain difficulty assessment.
    /// </summary>
    public static TerrainDifficultyScore ComputeTerrainDifficulty(
        List<TrackPoint> points, double distanceM, double elevGainM)
    {
        if (points.Count < 2 || distanceM < 1)
        {
            return new TerrainDifficultyScore
            {
                Score = 1,
                Grade = "Easy",
            };
        }

        // Compute per-segment grades
        var grades = new List<double>();
        var segmentDists = new List<double>();

        for (int i = 1; i < points.Count; i++)
        {
            var dist = points[i].DistFromPrev;
            if (dist < MinGradeSegmentM) continue;

            var dEle = points[i].Ele - points[i - 1].Ele;
            var grade = Math.Abs(dEle / dist) * 100.0; // in percent
            grades.Add(grade);
            segmentDists.Add(dist);
        }

        if (grades.Count == 0)
        {
            return new TerrainDifficultyScore
            {
                Score = 1,
                Grade = "Easy",
            };
        }

        var totalSegmentDist = segmentDists.Sum();
        // Distance-weighted: an unweighted mean lets the many near-stationary
        // samples dominate the few long segments that carry the real terrain.
        var avgGrade = totalSegmentDist > 0
            ? grades.Zip(segmentDists, (g, d) => g * d).Sum() / totalSegmentDist
            : 0;
        var maxGrade = grades.Max();

        // Grade variance, weighted by the same distances as the mean
        var mean = avgGrade;
        var variance = totalSegmentDist > 0
            ? grades.Zip(segmentDists, (g, d) => (g - mean) * (g - mean) * d).Sum() / totalSegmentDist
            : 0;

        // Steep section ratio (% of distance where grade > 15%)
        double steepDist = 0;
        for (int i = 0; i < grades.Count; i++)
        {
            if (grades[i] > 15)
                steepDist += segmentDists[i];
        }
        var steepRatio = totalSegmentDist > 0 ? steepDist / totalSegmentDist : 0;

        // Elevation per km
        var distKm = distanceM / 1000.0;
        var elevPerKm = distKm > 0 ? elevGainM / distKm : 0;

        // Composite score with weighted normalization
        // Reference values for normalization (typical upper bounds)
        var normElevPerKm = Normalize(elevPerKm, 0, 200);     // 0-200 m/km range
        var normMaxGrade = Normalize(maxGrade, 0, 60);         // 0-60% range
        var normVariance = Normalize(variance, 0, 200);        // 0-200 range
        var normSteepRatio = Normalize(steepRatio, 0, 0.5);    // 0-50% range

        // Weights: elevPerKm=0.35, maxGrade=0.25, variance=0.20, steepRatio=0.20
        var rawScore = 0.35 * normElevPerKm
                     + 0.25 * normMaxGrade
                     + 0.20 * normVariance
                     + 0.20 * normSteepRatio;

        // Scale to 1-10
        var score = Math.Round(1 + rawScore * 9, 1);
        score = Math.Clamp(score, 1, 10);

        return new TerrainDifficultyScore
        {
            Score = score,
            Grade = ScoreToGrade(score),
            AvgGradePercent = Math.Round(avgGrade, 1),
            MaxGradePercent = Math.Round(maxGrade, 1),
            GradeVariance = Math.Round(variance, 2),
            SteepSectionRatio = Math.Round(steepRatio, 3),
            ElevationPerKm = Math.Round(elevPerKm, 1),
        };
    }

    // ── Aggregate computation ──

    /// <summary>
    /// Computes all effort metrics from processed points and summary data.
    /// </summary>
    public static EffortMetrics ComputeAll(List<TrackPoint> points, Summary summary)
    {
        var distM = summary.TotalDistance;
        var distKm = distM / 1000.0;
        var elevGain = summary.Elevation.Gain;
        var elevLoss = summary.Elevation.Loss;

        var naismith = NaismithTime(distM, elevGain);
        var tobler = ToblerTime(points);
        var munter = MunterTime(distM, elevGain, elevLoss);

        var movingSeconds = summary.MovingTime.TotalSeconds;

        return new EffortMetrics
        {
            NaismithTime = naismith,
            ToblerTime = tobler,
            MunterTime = munter,
            PerformanceRatioNaismith = naismith.TotalSeconds > 0
                ? Math.Round(movingSeconds / naismith.TotalSeconds, 2) : 0,
            PerformanceRatioTobler = tobler.TotalSeconds > 0
                ? Math.Round(movingSeconds / tobler.TotalSeconds, 2) : 0,
            KilometreEffort = Math.Round(KilometreEffort(distKm, elevGain, elevLoss), 1),
            ItraPoints = Math.Round(ItraPoints(distKm, elevGain), 1),
            ItraCategory = ItraCategory(ItraPoints(distKm, elevGain)),
            EquivalentFlatDistanceKm = Math.Round(EquivalentFlatDistance(points) / 1000.0, 1),
            TerrainDifficulty = ComputeTerrainDifficulty(points, distM, elevGain),
        };
    }

    // ── Helpers ──

    private static double Normalize(double value, double min, double max)
    {
        if (max <= min) return 0;
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    private static string ScoreToGrade(double score) => score switch
    {
        <= 2.5 => "Easy",
        <= 4.5 => "Moderate",
        <= 6.5 => "Hard",
        <= 8.5 => "Expert",
        _ => "Extreme",
    };
}
