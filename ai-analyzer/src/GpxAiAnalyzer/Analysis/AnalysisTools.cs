namespace GpxAiAnalyzer.Analysis;

using System.ComponentModel;

/// <summary>
/// Pure functions exposed as AI agent tools via AIFunctionFactory.
/// The agent calls these to compute derived metrics before forming its assessment.
/// </summary>
public static class AnalysisTools
{
    [Description("Calculate the average grade (steepness) as a percentage from elevation gain and horizontal distance.")]
    public static double GetSteepnessRatio(
        [Description("Total elevation gain in meters")] double elevationGainM,
        [Description("Total horizontal distance in kilometers")] double distanceKm)
    {
        return distanceKm > 0 ? elevationGainM / (distanceKm * 1000) * 100 : 0;
    }

    [Description("Classify the activity type based on speed and elevation metrics.")]
    public static string ClassifyActivity(
        [Description("Average moving speed in km/h")] double avgMovingSpeedKmh,
        [Description("Total elevation gain in meters")] double elevationGainM,
        [Description("Total distance in kilometers")] double distanceKm)
    {
        if (avgMovingSpeedKmh > 15) return "cycling";
        if (avgMovingSpeedKmh > 8) return "trail-running";
        if (distanceKm > 0 && elevationGainM / distanceKm > 100) return "mountaineering";
        return "hiking";
    }

    [Description("Estimate difficulty on a 1-10 scale using an ITRA effort-distance approximation.")]
    public static int EstimateDifficulty(
        [Description("Total distance in kilometers")] double distanceKm,
        [Description("Total elevation gain in meters")] double elevationGainM,
        [Description("Moving time in hours")] double movingTimeHours)
    {
        double effortKm = distanceKm + (elevationGainM / 100);
        return Math.Clamp((int)(effortKm / 5), 1, 10);
    }

    [Description("Calculate stop frequency: average number of stops per kilometer.")]
    public static double GetStopFrequency(
        [Description("Number of stops detected")] int stopCount,
        [Description("Total distance in kilometers")] double distanceKm)
    {
        return distanceKm > 0 ? stopCount / distanceKm : 0;
    }

    [Description("Estimate training stress score (TSS) from normalized power, threshold power, and duration.")]
    public static double EstimateTrainingStress(
        [Description("Normalized power in watts")] double normalizedPower,
        [Description("Functional threshold power in watts")] double thresholdPower,
        [Description("Duration in hours")] double durationHours)
    {
        if (thresholdPower <= 0) return 0;
        double intensityFactor = normalizedPower / thresholdPower;
        return intensityFactor * intensityFactor * durationHours * 100;
    }

    [Description("Classify heart rate training intensity based on percentage of time spent in high-intensity zones (Z4-Z5).")]
    public static string ClassifyIntensity(
        [Description("Percentage of total time spent in zones 4-5 (0-100)")] double highIntensityPercent)
    {
        if (highIntensityPercent > 50) return "high-intensity";
        if (highIntensityPercent > 20) return "moderate-intensity";
        return "low-intensity";
    }
}
