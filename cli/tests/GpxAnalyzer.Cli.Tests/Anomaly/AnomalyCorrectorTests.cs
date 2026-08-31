using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Anomaly;

public class AnomalyCorrectorTests
{
    private static DateTime T(int seconds) =>
        DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime().AddSeconds(seconds);

    // ---------------------------------------------------------------
    // ApplyFrozenSectionDistances: frozen section distance estimation
    // ---------------------------------------------------------------

    [Fact]
    public void ApplyFrozenSectionDistances_WithFrozenSection_EstimatesDistanceFromAvgSpeed()
    {
        // Simulate: 3 normal points → 3 frozen points → 1 normal point
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(60) },    // ~131m
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(120) },   // ~131m
            // Frozen section (same lat/lon)
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(180) },
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(240) },
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(300) },
            // Normal resumes
            new() { Lat = 48.003, Lon = 2.003, Ele = 100, Time = T(360) },   // ~131m
        };

        var frozenAnomaly = new TrackAnomaly
        {
            Type = AnomalyType.GpsFrozen,
            StartIndex = 3,
            EndIndex = 5,
            TimeImpactS = 180, // 3 points × 60s
            WasCorrected = true,
            Description = "GPS frozen for 3 minutes",
        };

        var summary = new Summary
        {
            TotalTime = TimeSpan.FromSeconds(360),
            MovingTime = TimeSpan.FromSeconds(360),
            AnomalyReport = new AnomalyReport
            {
                Anomalies = [frozenAnomaly],
                CorrectionApplied = true,
            },
        };

        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);

        // Frozen points should have estimated DistFromPrev > 0
        Assert.True(points[3].DistFromPrev > 0, "Frozen point should have estimated distance");
        Assert.True(points[4].DistFromPrev > 0, "Frozen point should have estimated distance");
        Assert.True(points[5].DistFromPrev > 0, "Frozen point should have estimated distance");

        // Total distance should be greater than just healthy sections
        double healthyDistance = points[1].DistFromPrev + points[2].DistFromPrev + points[6].DistFromPrev;
        double total = points.Sum(p => p.DistFromPrev);
        Assert.True(total > healthyDistance,
            "Total distance should include estimated frozen section distance");
    }

    [Fact]
    public void ApplyFrozenSectionDistances_NoAnomalyReport_IsANoOp()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(60) },
        };

        var summary = new Summary
        {
            TotalTime = TimeSpan.FromSeconds(60),
            MovingTime = TimeSpan.FromSeconds(60),
        };

        // Should not throw even without AnomalyReport
        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);

        // Nothing to override, so EnrichPoints' own distances survive untouched.
        Assert.True(points[1].DistFromPrev > 0);
    }

    [Fact]
    public void ApplyFrozenSectionDistances_EmptyPoints_DoesNotCrash()
    {
        var points = new List<TrackPoint>();
        var summary = new Summary
        {
            TotalTime = TimeSpan.Zero,
            MovingTime = TimeSpan.Zero,
        };

        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);

        Assert.Empty(points);
    }

    [Fact]
    public void ApplyFrozenSectionDistances_SinglePoint_DoesNotCrash()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
        };

        var summary = new Summary
        {
            TotalTime = TimeSpan.Zero,
            MovingTime = TimeSpan.Zero,
        };

        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);

        Assert.Equal(0, points[0].DistFromPrev);
    }

    // ---------------------------------------------------------------
    // ApplyFrozenSectionDistances: frozen section edge cases
    // ---------------------------------------------------------------

    [Fact]
    public void ApplyFrozenSectionDistances_FrozenAtStart_HandlesGracefully()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },     // Frozen
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(60) },    // Frozen
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(120) },
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(180) },
        };

        var frozenAnomaly = new TrackAnomaly
        {
            Type = AnomalyType.GpsFrozen,
            StartIndex = 0,
            EndIndex = 1,
            TimeImpactS = 60,
            WasCorrected = true,
        };

        var summary = new Summary
        {
            TotalTime = TimeSpan.FromSeconds(180),
            MovingTime = TimeSpan.FromSeconds(180),
            AnomalyReport = new AnomalyReport
            {
                Anomalies = [frozenAnomaly],
                CorrectionApplied = true,
            },
        };

        // Should not throw for boundary case
        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);
        Assert.True(points.Sum(p => p.DistFromPrev) > 0);
    }

    [Fact]
    public void ApplyFrozenSectionDistances_FrozenAtEnd_HandlesGracefully()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(60) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(120) }, // Frozen
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(180) }, // Frozen
        };

        var frozenAnomaly = new TrackAnomaly
        {
            Type = AnomalyType.GpsFrozen,
            StartIndex = 2,
            EndIndex = 3,
            TimeImpactS = 120,
            WasCorrected = true,
        };

        var summary = new Summary
        {
            TotalTime = TimeSpan.FromSeconds(180),
            MovingTime = TimeSpan.FromSeconds(180),
            AnomalyReport = new AnomalyReport
            {
                Anomalies = [frozenAnomaly],
                CorrectionApplied = true,
            },
        };

        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);
        Assert.True(points.Sum(p => p.DistFromPrev) > 0);
    }

    [Fact]
    public void ApplyFrozenSectionDistances_MultipleFrozenSections_EstimatesAll()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(60) },
            // Frozen section 1
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(120) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(180) },
            // Resume
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(240) },
            // Frozen section 2
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(300) },
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(360) },
            // Resume
            new() { Lat = 48.003, Lon = 2.003, Ele = 100, Time = T(420) },
        };

        var summary = new Summary
        {
            TotalTime = TimeSpan.FromSeconds(420),
            MovingTime = TimeSpan.FromSeconds(420),
            AnomalyReport = new AnomalyReport
            {
                Anomalies =
                [
                    new TrackAnomaly { Type = AnomalyType.GpsFrozen, StartIndex = 2, EndIndex = 3, TimeImpactS = 120, WasCorrected = true },
                    new TrackAnomaly { Type = AnomalyType.GpsFrozen, StartIndex = 5, EndIndex = 6, TimeImpactS = 120, WasCorrected = true },
                ],
                CorrectionApplied = true,
            },
        };

        SpeedCalculator.EnrichPoints(points);
        AnomalyCorrector.ApplyFrozenSectionDistances(points, summary);

        // Both frozen sections should have estimated distances
        Assert.True(points[2].DistFromPrev > 0, "First frozen section should have estimated distance");
        Assert.True(points[5].DistFromPrev > 0, "Second frozen section should have estimated distance");
    }

    // ---------------------------------------------------------------
    // ApplyCorrections: basic correction tests
    // ---------------------------------------------------------------

    [Fact]
    public void ApplyCorrections_GpsFrozen_InterpolatesPositions()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            // Frozen
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(60) },
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(120) },
            // Resumes
            new() { Lat = 48.003, Lon = 2.003, Ele = 100, Time = T(180) },
        };

        var report = new AnomalyReport
        {
            Anomalies =
            [
                new TrackAnomaly
                {
                    Type = AnomalyType.GpsFrozen,
                    StartIndex = 1, EndIndex = 2,
                    TimeImpactS = 120, Description = "Frozen",
                },
            ],
        };

        var corrected = AnomalyCorrector.ApplyCorrections(points, report);

        // Positions should be interpolated between point 0 and point 3
        Assert.NotEqual(48.0, points[1].Lat);
        Assert.NotEqual(48.0, points[2].Lat);
        Assert.True(points[1].Lat > 48.0 && points[1].Lat < 48.003);
        Assert.True(points[2].Lat > points[1].Lat && points[2].Lat < 48.003);
        Assert.True(corrected.CorrectionApplied);
        Assert.True(corrected.Anomalies[0].WasCorrected);
    }

    [Fact]
    public void ApplyCorrections_GpsDrift_CollapsesToCentroid()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            // Drifting points
            new() { Lat = 48.0001, Lon = 2.0001, Ele = 100, Time = T(60) },
            new() { Lat = 48.0003, Lon = 2.0003, Ele = 100, Time = T(120) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(180) },
        };

        var report = new AnomalyReport
        {
            Anomalies =
            [
                new TrackAnomaly
                {
                    Type = AnomalyType.GpsDrift,
                    StartIndex = 1, EndIndex = 2,
                    Description = "Drift during stop",
                },
            ],
        };

        var corrected = AnomalyCorrector.ApplyCorrections(points, report);

        // Drifting points should be collapsed to centroid
        Assert.Equal(points[1].Lat, points[2].Lat);
        Assert.Equal(points[1].Lon, points[2].Lon);
        Assert.Equal(0, points[1].DistFromPrev);
        Assert.Equal(0, points[2].DistFromPrev);
        Assert.True(corrected.Anomalies[0].WasCorrected);
    }

    [Fact]
    public void ApplyCorrections_ElevationSpike_InterpolatesElevation()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 500, Time = T(60) },   // Spike
            new() { Lat = 48.002, Lon = 2.002, Ele = 102, Time = T(120) },
        };

        var report = new AnomalyReport
        {
            Anomalies =
            [
                new TrackAnomaly
                {
                    Type = AnomalyType.ElevationSpike,
                    StartIndex = 1, EndIndex = 1,
                    Description = "Elevation spike",
                },
            ],
        };

        AnomalyCorrector.ApplyCorrections(points, report);

        // Elevation should be interpolated between 100 and 102
        Assert.True(points[1].Ele >= 100 && points[1].Ele <= 102,
            $"Elevation should be interpolated, got {points[1].Ele}");
    }

    [Fact]
    public void ApplyCorrections_BackwardTime_FixesTimestamp()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(100) },
            new() { Lat = 48.001, Lon = 2.001, Ele = 100, Time = T(50) }, // Backward!
            new() { Lat = 48.002, Lon = 2.002, Ele = 100, Time = T(200) },
        };

        var report = new AnomalyReport
        {
            Anomalies =
            [
                new TrackAnomaly
                {
                    Type = AnomalyType.BackwardTime,
                    StartIndex = 1, EndIndex = 1,
                    Description = "Backward timestamp",
                },
            ],
        };

        AnomalyCorrector.ApplyCorrections(points, report);

        // Time should be fixed to previous + 1 second
        Assert.Equal(T(101), points[1].Time);
    }

    [Fact]
    public void ApplyCorrections_HeartRateOutOfRange_NullifiesHR()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Time = T(0), HeartRate = 120 },
            new() { Lat = 48.001, Lon = 2.001, Time = T(60), HeartRate = 250 }, // Out of range
            new() { Lat = 48.002, Lon = 2.002, Time = T(120), HeartRate = 130 },
        };

        var report = new AnomalyReport
        {
            Anomalies =
            [
                new TrackAnomaly
                {
                    Type = AnomalyType.HeartRateOutOfRange,
                    StartIndex = 1, EndIndex = 1,
                    Description = "HR out of range",
                },
            ],
        };

        AnomalyCorrector.ApplyCorrections(points, report);

        Assert.Null(points[1].HeartRate);
        Assert.Equal(120, points[0].HeartRate);
        Assert.Equal(130, points[2].HeartRate);
    }

    // ---------------------------------------------------------------
    // Post-correction recompute (#79, #81, #82, #83, #84)
    // ---------------------------------------------------------------

    // -- #82: a frozen run starting at index 0 must ramp linearly -------
    [Fact]
    public void CorrectGpsFrozen_RunStartingAtIndexZero_ProducesEvenlySpacedPoints()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int i = 0; i < 5; i++)
            points.Add(new TrackPoint { Lat = 48.0000, Lon = 2.0, Time = t0.AddSeconds(i), Cadence = 80 });
        points.Add(new TrackPoint { Lat = 48.0050, Lon = 2.0, Time = t0.AddSeconds(5), Cadence = 80 });

        var report = new AnomalyReport
        {
            Anomalies =
            [
                new TrackAnomaly
                {
                    Type = AnomalyType.GpsFrozen,
                    Severity = AnomalySeverity.Warning,
                    Category = AnomalyCategory.Position,
                    StartIndex = 0, EndIndex = 4,
                    StartTime = t0, EndTime = t0.AddSeconds(4),
                    TimeImpactS = 4,
                },
            ],
        };

        AnomalyCorrector.ApplyCorrections(points, report);

        // Five points interpolated between 48.0000 and 48.0050 must be evenly
        // spaced: every consecutive delta identical to within floating error.
        var deltas = new List<double>();
        for (int i = 1; i <= 4; i++) deltas.Add(points[i].Lat - points[i - 1].Lat);
        foreach (var d in deltas)
            Assert.Equal(deltas[0], d, 8);
    }

    // -- #83: a freeze longer than moving time must not zero the estimate
    [Fact]
    public void ApplyFrozenSectionDistances_FrozenLongerThanMovingTime_StillEstimatesDistance()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int i = 0; i < 10; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.0009, Lon = 2.0, Time = t0.AddSeconds(i * 30) });

        // Frozen indices 4..7 (900 s of impact) with only 300 s of moving time
        var s = new Summary
        {
            MovingTime = TimeSpan.FromSeconds(300),
            AnomalyReport = new AnomalyReport
            {
                Anomalies =
                [
                    new TrackAnomaly
                    {
                        Type = AnomalyType.GpsFrozen,
                        StartIndex = 4, EndIndex = 7,
                        TimeImpactS = 900, WasCorrected = true,
                    },
                ],
            },
        };
        SpeedCalculator.EnrichPoints(points);

        AnomalyCorrector.ApplyFrozenSectionDistances(points, s);

        double frozenDist = 0;
        for (int i = 4; i <= 7; i++) frozenDist += points[i].DistFromPrev;
        Assert.True(frozenDist > 0,
            "the frozen section must receive an estimated distance, not zero");
    }

    private static ComputeConfig BuildFixAnomaliesConfig(double maxReasonableSpeed) => new()
    {
        FixAnomalies = true,
        AnomalyConfig = AnomalyConfig.Default(),
        StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
        SmoothingLevel = "none",
        TrackSmoothing = "none",
        DemSource = null,
        ElevationCfg = new ElevationConfig(),
        BiometricsCfg = new BiometricsConfig(),
        MaxReasonableSpeed = maxReasonableSpeed,
    };

    // -- #81: the recompute must re-clamp ------------------------------
    [Fact]
    public void FixAnomalies_BackwardTimestamp_DoesNotReportAnImpossibleMaxSpeed()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0000, Lon = 2.0, Ele = 100, Time = t0 },
            // Backward timestamp, ~100 m further along
            new() { Lat = 48.0009, Lon = 2.0, Ele = 100, Time = t0.AddSeconds(-60) },
            new() { Lat = 48.0010, Lon = 2.0, Ele = 100, Time = t0.AddSeconds(30) },
        };
        for (int i = 3; i < 30; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0010 + (i - 2) * 0.00003, Lon = 2.0, Ele = 100,
                Time = t0.AddSeconds(30 + i),
            });

        var cfg = BuildFixAnomaliesConfig(maxReasonableSpeed: 4.0); // hiking
        var (summary, _) = ComputePipeline.Compute(points, 1, cfg);

        // CorrectBackwardTime sets p1.Time = p0.Time + 1s -> 100 m in 1 s.
        // Without a re-clamp that lands in speed.max as 360 km/h for a hike.
        Assert.True(summary.Speed.MaxSpeed <= 4.0,
            $"max speed should stay clamped at the hiking threshold, got {summary.Speed.MaxSpeed * 3.6:F0} km/h");
    }

}
