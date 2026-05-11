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

    // ---------------------------------------------------------------
    // Smoothed MaxSpeed (rolling window)
    // ---------------------------------------------------------------

    [Fact]
    public void MaxSpeedFromPoints_RollingWindow_SmoothsGpsNoise()
    {
        // Simulate 2 minutes of running at ~3 m/s with one noisy point at 6 m/s.
        // With 30s window, the max smoothed speed should be close to 3 m/s,
        // not the noisy 6 m/s.
        var points = new List<TrackPoint>();
        double lat = 48.0;
        for (int t = 0; t <= 120; t += 1)
        {
            double speed = 3.0; // m/s
            if (t == 60) speed = 6.0; // One noisy point
            double dist = speed * 1.0; // 1 second interval
            lat += dist / 111320.0; // approx degrees per meter
            points.Add(new TrackPoint
            {
                Lat = lat, Lon = 2.0, Ele = 100, Time = T(t),
            });
        }

        SpeedCalculator.EnrichPoints(points);

        double maxSmoothed = SpeedCalculator.MaxSpeedFromPoints(points, windowSeconds: 30);

        // Should be much less than the 6 m/s noise spike
        Assert.True(maxSmoothed < 4.0,
            $"Smoothed max speed should be ~3 m/s, not {maxSmoothed:F1} m/s (noise filtered)");
        Assert.True(maxSmoothed >= 2.5,
            $"Smoothed max speed should be at least 2.5 m/s, got {maxSmoothed:F1}");
    }

    [Fact]
    public void MaxSpeedFromPoints_RollingWindow_CapturesRealSpeedBurst()
    {
        // Simulate: 60s slow (2 m/s) → 30s fast (5 m/s) → 60s slow (2 m/s)
        // The 30s window should capture the fast section.
        var points = new List<TrackPoint>();
        double lat = 48.0;
        for (int t = 0; t <= 150; t += 1)
        {
            double speed = (t >= 60 && t < 90) ? 5.0 : 2.0;
            double dist = speed * 1.0;
            lat += dist / 111320.0;
            points.Add(new TrackPoint
            {
                Lat = lat, Lon = 2.0, Ele = 100, Time = T(t),
            });
        }

        SpeedCalculator.EnrichPoints(points);

        double maxSmoothed = SpeedCalculator.MaxSpeedFromPoints(points, windowSeconds: 30);

        // Should capture the fast burst (~5 m/s)
        Assert.True(maxSmoothed >= 4.0,
            $"Should capture the speed burst, got {maxSmoothed:F1} m/s");
    }

    [Fact]
    public void MaxSpeedFromPoints_ShortTrack_FallsBackToInstantaneous()
    {
        // Track shorter than 2x window → falls back to instantaneous max
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0), CalcSpeed = 3.0 },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(10), CalcSpeed = 5.0 },
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(20), CalcSpeed = 4.0 },
        };

        double maxSpeed = SpeedCalculator.MaxSpeedFromPoints(points, windowSeconds: 30);

        // Short track: should use instantaneous max (5.0 m/s)
        Assert.Equal(5.0, maxSpeed);
    }

    [Fact]
    public void MaxSpeedFromPoints_WindowZero_UsesInstantaneous()
    {
        var points = new List<TrackPoint>
        {
            new() { CalcSpeed = 3.0, Time = T(0) },
            new() { CalcSpeed = 10.0, Time = T(60) },
            new() { CalcSpeed = 4.0, Time = T(120) },
        };

        Assert.Equal(10.0, SpeedCalculator.MaxSpeedFromPoints(points, windowSeconds: 0));
    }
}
