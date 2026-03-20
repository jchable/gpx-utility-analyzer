namespace GpxAnalyzer.Api.Services;

/// <summary>
/// Estimates caloric expenditure for a GPX activity.
/// Uses heart rate (Keytel 2005) when profile data is available, falls back to MET otherwise.
/// </summary>
public static class CalorieCalculator
{
    // MET values [slow, moderate, fast] per activity type
    private static readonly Dictionary<string, (double Slow, double Moderate, double Fast)> MetTable = new()
    {
        ["run"]   = (8.0, 10.0, 12.5),
        ["trail"] = (9.0, 11.0, 14.0),
        ["hike"]  = (5.5,  7.0,  8.5),
        ["cycle"] = (6.0,  8.0, 12.0),
        ["walk"]  = (3.0,  4.0,  5.0),
        ["swim"]  = (6.0,  8.0, 10.0),
        ["other"] = (5.0,  7.0,  9.0),
    };

    // Typical average moving speed per activity type (km/h) — used for intensity classification
    private static readonly Dictionary<string, double> TypicalSpeed = new()
    {
        ["run"]   = 10.0,
        ["trail"] =  8.0,
        ["hike"]  =  4.0,
        ["cycle"] = 20.0,
        ["walk"]  =  5.0,
        ["swim"]  =  3.0,
        ["other"] =  6.0,
    };

    /// <summary>
    /// Computes estimated calorie expenditure.
    /// </summary>
    /// <param name="activityType">Activity type key (run, trail, hike, cycle, walk, swim, other)</param>
    /// <param name="movingTimeSeconds">Moving time in seconds</param>
    /// <param name="elevGainM">Total elevation gain in meters</param>
    /// <param name="distanceKm">Total distance in km</param>
    /// <param name="avgMovingSpeedKmh">Average moving speed in km/h (for MET intensity)</param>
    /// <param name="avgHrBpm">Average heart rate in bpm (nullable)</param>
    /// <param name="weightKg">Athlete weight in kg (nullable)</param>
    /// <param name="sex">Athlete sex: "male" | "female" | other (nullable)</param>
    /// <param name="age">Athlete age in years (nullable)</param>
    /// <returns>Tuple of (estimated kcal, method: "hr" | "met")</returns>
    public static (double Kcal, string Method) Compute(
        string activityType,
        double movingTimeSeconds,
        double elevGainM,
        double distanceKm,
        double avgMovingSpeedKmh,
        double? avgHrBpm,
        double? weightKg,
        string? sex,
        int? age)
    {
        if (movingTimeSeconds <= 0)
            return (0, "met");

        // Method 1: Heart rate (Keytel et al. 2005) — requires HR + weight + age
        if (avgHrBpm.HasValue && weightKg.HasValue && age.HasValue)
        {
            var kcal = ComputeFromHeartRate(
                avgHrBpm.Value, weightKg.Value, age.Value,
                sex, movingTimeSeconds);
            return (Math.Round(kcal, 0), "hr");
        }

        // Method 2: MET fallback
        {
            var effectiveWeight = weightKg ?? DefaultWeight(sex);
            var kcal = ComputeFromMet(activityType, movingTimeSeconds, elevGainM, distanceKm, avgMovingSpeedKmh, effectiveWeight);
            return (Math.Round(kcal, 0), "met");
        }
    }

    // ─── Private helpers ────────────────────────────────────────────────────────

    private static double ComputeFromHeartRate(
        double avgHrBpm, double weightKg, int age, string? sex, double movingTimeSeconds)
    {
        var durationMin = movingTimeSeconds / 60.0;
        double kcalPerMin;

        if (string.Equals(sex, "female", StringComparison.OrdinalIgnoreCase))
            kcalPerMin = (-20.4022 + 0.4472 * avgHrBpm + 0.1263 * weightKg + 0.0740 * age) / 4.184;
        else
            kcalPerMin = (-55.0969 + 0.6309 * avgHrBpm + 0.1988 * weightKg + 0.2017 * age) / 4.184;

        // Clamp to positive value (formula can produce negative for very low HR)
        return Math.Max(0, kcalPerMin * durationMin);
    }

    private static double ComputeFromMet(
        string activityType, double movingTimeSeconds, double elevGainM,
        double distanceKm, double avgMovingSpeedKmh, double weightKg)
    {
        var type = MetTable.ContainsKey(activityType) ? activityType : "other";
        var (slow, moderate, fast) = MetTable[type];

        // Determine intensity by comparing actual speed to typical speed
        var typicalSpeed = TypicalSpeed.TryGetValue(type, out var ts) ? ts : 6.0;
        double ratio = typicalSpeed > 0 ? avgMovingSpeedKmh / typicalSpeed : 1.0;

        double met = ratio < 0.6 ? slow
                   : ratio > 1.2 ? fast
                   : moderate;

        // Elevation adjustment for trail/hike
        if ((type == "trail" || type == "hike") && distanceKm > 0)
        {
            double elevPerKm = elevGainM / distanceKm;
            met *= 1.0 + elevPerKm / 100.0 * 0.1;
        }

        var durationHours = movingTimeSeconds / 3600.0;
        return met * weightKg * durationHours;
    }

    private static double DefaultWeight(string? sex) =>
        string.Equals(sex, "female", StringComparison.OrdinalIgnoreCase) ? 60.0 : 70.0;
}
