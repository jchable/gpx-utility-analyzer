using GpxAnalyzer.Api.Services;

namespace GpxAnalyzer.Api.Tests.Enrichment;

/// <summary>
/// Unit tests for CalorieCalculator — pure logic, no infrastructure required.
/// </summary>
public class CalorieCalculatorTests
{
    // ─── Method selection ────────────────────────────────────────────────────

    [Fact]
    public void Compute_NoProfile_UsesMet_ReturnsPositive()
    {
        var (kcal, method) = CalorieCalculator.Compute(
            activityType: "trail",
            movingTimeSeconds: 3600,
            elevGainM: 500,
            distanceKm: 10,
            avgMovingSpeedKmh: 8,
            avgHrBpm: null,
            weightKg: null,
            sex: null,
            age: null);

        Assert.Equal("met", method);
        Assert.True(kcal > 0);
    }

    [Fact]
    public void Compute_WithCompleteProfile_UsesHeartRate()
    {
        var (kcal, method) = CalorieCalculator.Compute(
            activityType: "run",
            movingTimeSeconds: 3600,
            elevGainM: 0,
            distanceKm: 10,
            avgMovingSpeedKmh: 10,
            avgHrBpm: 150,
            weightKg: 70,
            sex: "male",
            age: 35);

        Assert.Equal("hr", method);
        Assert.True(kcal > 0);
    }

    [Fact]
    public void Compute_HrPresentButWeightMissing_FallsToMet()
    {
        var (_, method) = CalorieCalculator.Compute(
            activityType: "run",
            movingTimeSeconds: 3600,
            elevGainM: 0,
            distanceKm: 10,
            avgMovingSpeedKmh: 10,
            avgHrBpm: 150,    // HR present
            weightKg: null,   // but no weight
            sex: "male",
            age: 35);

        Assert.Equal("met", method);
    }

    [Fact]
    public void Compute_HrPresentButAgeMissing_FallsToMet()
    {
        var (_, method) = CalorieCalculator.Compute(
            activityType: "run",
            movingTimeSeconds: 3600,
            elevGainM: 0,
            distanceKm: 10,
            avgMovingSpeedKmh: 10,
            avgHrBpm: 150,
            weightKg: 70,
            sex: "male",
            age: null); // age missing

        Assert.Equal("met", method);
    }

    // ─── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public void Compute_ZeroMovingTime_ReturnsZeroKcal()
    {
        var (kcal, _) = CalorieCalculator.Compute(
            activityType: "trail",
            movingTimeSeconds: 0,
            elevGainM: 500,
            distanceKm: 10,
            avgMovingSpeedKmh: 8,
            avgHrBpm: null,
            weightKg: null,
            sex: null,
            age: null);

        Assert.Equal(0, kcal);
    }

    // ─── MET intensity levels ────────────────────────────────────────────────

    [Fact]
    public void Compute_FastPace_MoreCaloriesThanSlowPace()
    {
        // Same duration, same weight, but different speeds → different MET → different calories
        var (kcalFast, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 15, avgMovingSpeedKmh: 15, // fast (150% of typical 10)
            null, 70, null, null);

        var (kcalSlow, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 5, avgMovingSpeedKmh: 5,  // slow (50% of typical 10)
            null, 70, null, null);

        Assert.True(kcalFast > kcalSlow, $"Fast ({kcalFast}) should burn more than slow ({kcalSlow})");
    }

    // ─── Trail elevation adjustment ──────────────────────────────────────────

    [Fact]
    public void Compute_Trail_HighElevation_MoreCaloriesThanFlat()
    {
        // Same distance and time, but one has significant elevation gain
        var (kcalHilly, _) = CalorieCalculator.Compute(
            "trail", 3600, elevGainM: 800, distanceKm: 8, avgMovingSpeedKmh: 8,
            null, 70, null, null);

        var (kcalFlat, _) = CalorieCalculator.Compute(
            "trail", 3600, elevGainM: 0, distanceKm: 8, avgMovingSpeedKmh: 8,
            null, 70, null, null);

        Assert.True(kcalHilly > kcalFlat, $"Hilly ({kcalHilly}) should burn more than flat ({kcalFlat})");
    }

    // ─── Default weights ─────────────────────────────────────────────────────

    [Fact]
    public void Compute_FemaleNoWeight_UsesLighterDefault_ThanMaleNoWeight()
    {
        // Female default (60kg) should burn fewer calories than male default (70kg)
        var (kcalFemale, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 10, 10, null, null, "female", null);

        var (kcalMale, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 10, 10, null, null, "male", null);

        Assert.True(kcalFemale < kcalMale, $"Female default ({kcalFemale}) should be less than male default ({kcalMale})");
    }

    [Fact]
    public void Compute_NullSexNoWeight_TreatedAsMaleDefault()
    {
        // sex=null → 70kg default (same as male)
        var (kcalNull, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 10, 10, null, null, sex: null, null);

        var (kcalMale, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 10, 10, null, null, sex: "male", null);

        Assert.Equal(kcalMale, kcalNull);
    }

    // ─── HR formula sanity check ─────────────────────────────────────────────

    [Fact]
    public void Compute_HrMethod_HighHR_MoreCaloriesThanLowHR()
    {
        var (kcalHigh, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 10, 10, avgHrBpm: 180, 70, "male", 35);

        var (kcalLow, _) = CalorieCalculator.Compute(
            "run", 3600, 0, 10, 10, avgHrBpm: 120, 70, "male", 35);

        Assert.True(kcalHigh > kcalLow, $"High HR ({kcalHigh}) should burn more than low HR ({kcalLow})");
    }
}
