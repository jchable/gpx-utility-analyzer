using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class SpeedCalculatorTests
{
    [Fact]
    public void ComputeSpeed_NormalTrack_ReturnsPositive()
    {
        double dist = 1000;
        var totalTime = TimeSpan.FromMinutes(20);
        var movingTime = TimeSpan.FromMinutes(18);
        var result = SpeedCalculator.ComputeSpeed(dist, totalTime, movingTime);
        Assert.True(result.AvgSpeed > 0);
        Assert.True(result.AvgMovingSpeed > 0);
    }

    [Fact]
    public void ComputeSpeed_ZeroTime_ReturnsZero()
    {
        var result = SpeedCalculator.ComputeSpeed(1000, TimeSpan.Zero, TimeSpan.Zero);
        Assert.Equal(0, result.AvgSpeed);
    }

    [Fact]
    public void EnrichPoints_SetsDistFromPrevAndCalcSpeed()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Lat = 48.001, Lon = 2.001, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
        };
        SpeedCalculator.EnrichPoints(points);
        Assert.Equal(0, points[0].DistFromPrev);
        Assert.True(points[1].DistFromPrev > 0);
        Assert.True(points[1].CalcSpeed > 0);
    }

    [Fact]
    public void ClampSpeeds_ClampsExcessiveSpeed()
    {
        var points = new List<TrackPoint>
        {
            new() { CalcSpeed = 5.0 },
            new() { CalcSpeed = 100.0 },
            new() { CalcSpeed = 5.0 },
        };
        SpeedCalculator.ClampSpeeds(points, 25.0);
        Assert.Equal(5.0, points[0].CalcSpeed);
        Assert.Equal(0.0, points[1].CalcSpeed);
        Assert.Equal(5.0, points[2].CalcSpeed);
    }

    [Fact]
    public void PresetMaxSpeed_ContainsAllPresets()
    {
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("hiking"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("trail"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("cycling"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("running"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("swimming"));
        Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey("walking"));
    }

    [Theory]
    [InlineData("hiking", 4.0)]
    [InlineData("trail", 7.0)]
    [InlineData("cycling", 25.0)]
    [InlineData("running", 7.0)]
    [InlineData("swimming", 3.0)]
    [InlineData("walking", 4.0)]
    public void PresetMaxSpeed_HasExpectedValues(string preset, double expected)
    {
        Assert.Equal(expected, SpeedCalculator.PresetMaxSpeed[preset]);
    }

    [Fact]
    public void PresetMaxSpeed_MatchesStopDetectorPresets()
    {
        foreach (var preset in StopDetector.Presets.Keys)
        {
            Assert.True(SpeedCalculator.PresetMaxSpeed.ContainsKey(preset),
                $"SpeedCalculator.PresetMaxSpeed missing preset '{preset}'");
        }
    }

    // ---------------------------------------------------------------
    // Sequence: EnrichPoints → ClampSpeeds → MaxSpeedFromPoints
    // These tests prevent the regression where clamped values get
    // overwritten by a subsequent EnrichPoints call.
    // ---------------------------------------------------------------

    private static DateTime T(int seconds) =>
        DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime().AddSeconds(seconds);

    [Fact]
    public void MaxSpeedFromPoints_AfterEnrichThenClamp_RespectsThreshold()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = T(0) },
            new() { Lat = 48.0001, Lon = 2.0001, Time = T(10) },   // ~15.7 m/s → normal
            new() { Lat = 49.0, Lon = 3.0, Time = T(11) },         // ~157,000 m/s → outlier
            new() { Lat = 48.0003, Lon = 2.0003, Time = T(20) },   // normal
        };

        SpeedCalculator.EnrichPoints(points);

        // Verify outlier was calculated
        Assert.True(points[2].CalcSpeed > 1000, "Outlier should have very high CalcSpeed");

        SpeedCalculator.ClampSpeeds(points, 25.0);

        // Verify clamp
        Assert.Equal(0, points[2].CalcSpeed);
        Assert.Equal(0, points[2].DistFromPrev);

        double maxSpeed = SpeedCalculator.MaxSpeedFromPoints(points);
        Assert.True(maxSpeed <= 25.0,
            $"MaxSpeed after clamping should be <= 25.0, got {maxSpeed:F1}");
    }

    [Fact]
    public void ClampSpeeds_ZerosDistFromPrev_AffectsTotalDistance()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = T(0) },
            new() { Lat = 48.0001, Lon = 2.0001, Time = T(60) },   // Normal: ~15.6m in 60s
            new() { Lat = 49.0, Lon = 3.0, Time = T(61) },         // Outlier: huge distance
            new() { Lat = 48.0003, Lon = 2.0003, Time = T(120) },
        };

        SpeedCalculator.EnrichPoints(points);
        double distBeforeClamp = points.Sum(p => p.DistFromPrev);

        SpeedCalculator.ClampSpeeds(points, 25.0);
        double distAfterClamp = points.Sum(p => p.DistFromPrev);

        Assert.True(distAfterClamp < distBeforeClamp,
            "Total distance should decrease after clamping outlier");
    }

    [Fact]
    public void EnrichPoints_ThenClamp_ThenEnrichAgain_OverwritesClamped()
    {
        // Demonstrates exactly WHY RecalculateStats needs to re-clamp:
        // calling EnrichPoints a second time undoes the clamp.
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = T(0) },
            new() { Lat = 49.0, Lon = 3.0, Time = T(1) },  // Outlier
            new() { Lat = 48.001, Lon = 2.001, Time = T(60) },
        };

        // First pass
        SpeedCalculator.EnrichPoints(points);
        SpeedCalculator.ClampSpeeds(points, 25.0);
        Assert.Equal(0.0, points[1].CalcSpeed); // Should be clamped

        // Second EnrichPoints (like RecalculateStats does)
        SpeedCalculator.EnrichPoints(points);

        // This proves the bug: CalcSpeed is back to the outlier value
        Assert.True(points[1].CalcSpeed > 1000,
            "Re-enrichment overwrites clamped values — this is why RecalculateStats must re-clamp");
    }

    [Fact]
    public void ClampSpeeds_Disabled_DoesNotModify()
    {
        var points = new List<TrackPoint>
        {
            new() { CalcSpeed = 5.0, DistFromPrev = 50 },
            new() { CalcSpeed = 100.0, DistFromPrev = 100 },
        };

        int clamped = SpeedCalculator.ClampSpeeds(points, 0); // maxSpeed=0 disables

        Assert.Equal(0, clamped);
        Assert.Equal(100.0, points[1].CalcSpeed);
        Assert.Equal(100, points[1].DistFromPrev);
    }

    [Fact]
    public void MaxSpeedFromPoints_EmptyList_ReturnsZero()
    {
        Assert.Equal(0, SpeedCalculator.MaxSpeedFromPoints([]));
    }

    [Fact]
    public void MaxSpeedFromPoints_AllClamped_ReturnsZero()
    {
        var points = new List<TrackPoint>
        {
            new() { CalcSpeed = 0 },
            new() { CalcSpeed = 0 },
        };

        Assert.Equal(0, SpeedCalculator.MaxSpeedFromPoints(points));
    }

    [Fact]
    public void EnrichPoints_SimultaneousTimestamps_ZeroSpeed()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = T(0) },
            new() { Lat = 48.001, Lon = 2.001, Time = T(0) }, // Same timestamp
        };

        SpeedCalculator.EnrichPoints(points);

        // DistFromPrev should be set but CalcSpeed should be 0 (can't divide by 0)
        Assert.True(points[1].DistFromPrev > 0);
        Assert.Equal(0, points[1].CalcSpeed);
    }
}
