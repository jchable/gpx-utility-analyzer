using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class EffortCalculatorTests
{
    // ── Naismith ──

    [Fact]
    public void NaismithTime_FlatTerrain_BasedOnSpeed()
    {
        // 10 km flat → 10/5 = 2 hours
        var result = EffortCalculator.NaismithTime(10_000, 0);
        Assert.Equal(2.0, result.TotalHours, 2);
    }

    [Fact]
    public void NaismithTime_WithElevation_AddsTime()
    {
        // 10 km + 600 m D+ → 2h + 1h = 3h
        var result = EffortCalculator.NaismithTime(10_000, 600);
        Assert.Equal(3.0, result.TotalHours, 2);
    }

    [Fact]
    public void NaismithTime_ZeroDistance_ZeroTime()
    {
        var result = EffortCalculator.NaismithTime(0, 0);
        Assert.Equal(TimeSpan.Zero, result);
    }

    // ── Tobler ──

    [Fact]
    public void ToblerSpeed_FlatTerrain_ApproxFiveKmh()
    {
        // At grade 0: V = 6 * exp(-3.5 * 0.05) = 6 * exp(-0.175) ≈ 5.04 km/h
        var speed = EffortCalculator.ToblerSpeed(0);
        Assert.InRange(speed, 4.9, 5.2);
    }

    [Fact]
    public void ToblerSpeed_OptimalDownhill_MaxSpeed()
    {
        // Optimal at grade -0.05: V = 6 * exp(0) = 6.0 km/h
        var speed = EffortCalculator.ToblerSpeed(-0.05);
        Assert.Equal(6.0, speed, 2);
    }

    [Fact]
    public void ToblerSpeed_SteepUphill_SlowSpeed()
    {
        // At 30% grade: significantly slower
        var speed = EffortCalculator.ToblerSpeed(0.30);
        Assert.True(speed < 2.0);
    }

    [Fact]
    public void ToblerTime_FlatTrack_ReasonableTime()
    {
        // 10 points, 1 km total, flat
        var points = CreateFlatTrack(10, 1000);
        var result = EffortCalculator.ToblerTime(points);
        // ~1 km at ~5 km/h ≈ 12 min
        Assert.InRange(result.TotalMinutes, 10, 15);
    }

    [Fact]
    public void ToblerTime_EmptyPoints_ReturnsZero()
    {
        var result = EffortCalculator.ToblerTime([]);
        Assert.Equal(TimeSpan.Zero, result);
    }

    // ── Munter ──

    [Fact]
    public void MunterTime_FlatTerrain_HorizComponent()
    {
        // 8 km flat → 8/4 = 2h horiz, max(2,0)+min(2,0)/2 = 2h + descent 0 = 2h
        var result = EffortCalculator.MunterTime(8_000, 0, 0);
        Assert.Equal(2.0, result.TotalHours, 2);
    }

    [Fact]
    public void MunterTime_SteepAscent_AscentDominates()
    {
        // 4 km + 1200m D+ → horiz=1h, ascent=3h → max(3,1)+min(3,1)/2 = 3.5h + descent 0
        var result = EffortCalculator.MunterTime(4_000, 1200, 0);
        Assert.Equal(3.5, result.TotalHours, 2);
    }

    [Fact]
    public void MunterTime_WithDescent_AddsDescentTime()
    {
        // 4 km + 0 D+ + 800m D- → horiz=1h, ascent=0h → max(1,0)+min(1,0)/2=1h + descent=1h = 2h
        var result = EffortCalculator.MunterTime(4_000, 0, 800);
        Assert.Equal(2.0, result.TotalHours, 2);
    }

    // ── Kilomètre-effort ──

    [Fact]
    public void KilometreEffort_FlatTerrain_EqualsDistance()
    {
        var ke = EffortCalculator.KilometreEffort(10, 0, 0);
        Assert.Equal(10.0, ke, 2);
    }

    [Fact]
    public void KilometreEffort_WithElevation_AddsEffort()
    {
        // 10 km + 500m D+ + 300m D- → 10 + 5 + 2 = 17
        var ke = EffortCalculator.KilometreEffort(10, 500, 300);
        Assert.Equal(17.0, ke, 2);
    }

    // ── ITRA ──

    [Fact]
    public void ItraPoints_BasicCalculation()
    {
        // 42 km + 2500m D+ → 42 + 25 = 67
        var pts = EffortCalculator.ItraPoints(42, 2500);
        Assert.Equal(67.0, pts, 2);
    }

    [Theory]
    [InlineData(10, "XXS")]    // < 25
    [InlineData(30, "XS")]     // 25-44
    [InlineData(50, "S")]      // 45-64
    [InlineData(75, "M")]      // 65-89
    [InlineData(100, "L")]     // 90-119
    [InlineData(140, "XL")]    // 120-159
    [InlineData(200, "XXL")]   // ≥ 160
    public void ItraCategory_CorrectMapping(double points, string expected)
    {
        Assert.Equal(expected, EffortCalculator.ItraCategory(points));
    }

    // ── Equivalent Flat Distance ──

    [Fact]
    public void EquivalentFlatDistance_FlatTrack_EqualsDistance()
    {
        var points = CreateFlatTrack(10, 1000);
        var efd = EffortCalculator.EquivalentFlatDistance(points);
        // On flat terrain, EFD should approximately equal actual distance
        Assert.InRange(efd, 900, 1100);
    }

    [Fact]
    public void EquivalentFlatDistance_UphillTrack_GreaterThanActual()
    {
        var points = CreateUphillTrack(10, 1000, 200);
        var efd = EffortCalculator.EquivalentFlatDistance(points);
        // Uphill should cost more than flat
        Assert.True(efd > 1000);
    }

    [Fact]
    public void EquivalentFlatDistance_EmptyPoints_ReturnsZero()
    {
        Assert.Equal(0, EffortCalculator.EquivalentFlatDistance([]));
    }

    // ── Terrain Difficulty ──

    [Fact]
    public void TerrainDifficulty_FlatTerrain_Easy()
    {
        var points = CreateFlatTrack(20, 5000);
        var score = EffortCalculator.ComputeTerrainDifficulty(points, 5000, 0);
        Assert.Equal("Easy", score.Grade);
        Assert.InRange(score.Score, 1, 3);
    }

    [Fact]
    public void TerrainDifficulty_SteepTerrain_Higher()
    {
        var points = CreateUphillTrack(20, 2000, 600);
        var score = EffortCalculator.ComputeTerrainDifficulty(points, 2000, 600);
        Assert.True(score.Score > 3);
        Assert.True(score.ElevationPerKm > 200);
    }

    [Fact]
    public void TerrainDifficulty_InsufficientPoints_ReturnsEasy()
    {
        var score = EffortCalculator.ComputeTerrainDifficulty([], 0, 0);
        Assert.Equal(1, score.Score);
        Assert.Equal("Easy", score.Grade);
    }

    // ── Minetti Model ──

    [Fact]
    public void MinettiCost_FlatGrade_ApproxThreePointSix()
    {
        var cost = MinettiModel.Cost(0);
        Assert.InRange(cost, 3.5, 3.7);
    }

    [Fact]
    public void MinettiCost_UphillCostsMore()
    {
        var flat = MinettiModel.Cost(0);
        var uphill = MinettiModel.Cost(0.20);
        Assert.True(uphill > flat);
    }

    [Fact]
    public void MinettiCost_ClampsExtremeGrade()
    {
        // Beyond ±0.45 should be clamped
        var extreme = MinettiModel.Cost(1.0);
        var clamped = MinettiModel.Cost(0.45);
        Assert.Equal(clamped, extreme, 4);
    }

    // ── ComputeAll ──

    [Fact]
    public void ComputeAll_ProducesAllMetrics()
    {
        var points = CreateUphillTrack(20, 5000, 300);
        var summary = new Summary
        {
            TotalDistance = 5000,
            Elevation = new ElevationResult { Gain = 300, Loss = 100, Max = 500, Min = 200 },
            MovingTime = TimeSpan.FromHours(2),
            TotalTime = TimeSpan.FromHours(2.5),
        };

        var effort = EffortCalculator.ComputeAll(points, summary);

        Assert.True(effort.NaismithTime > TimeSpan.Zero);
        Assert.True(effort.ToblerTime > TimeSpan.Zero);
        Assert.True(effort.MunterTime > TimeSpan.Zero);
        Assert.True(effort.KilometreEffort > 0);
        Assert.True(effort.ItraPoints > 0);
        Assert.False(string.IsNullOrEmpty(effort.ItraCategory));
        Assert.True(effort.EquivalentFlatDistanceKm > 0);
        Assert.True(effort.TerrainDifficulty.Score >= 1);
        Assert.False(string.IsNullOrEmpty(effort.TerrainDifficulty.Grade));
    }

    // ── Helpers ──

    private static List<TrackPoint> CreateFlatTrack(int count, double totalDistM)
    {
        var points = new List<TrackPoint>();
        var segDist = totalDistM / (count - 1);
        for (int i = 0; i < count; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 45.0 + i * 0.001,
                Lon = 6.0,
                Ele = 500,
                DistFromPrev = i == 0 ? 0 : segDist,
                Time = DateTime.UtcNow.AddSeconds(i * 60),
            });
        }
        return points;
    }

    private static List<TrackPoint> CreateUphillTrack(int count, double totalDistM, double totalElevGain)
    {
        var points = new List<TrackPoint>();
        var segDist = totalDistM / (count - 1);
        var segElev = totalElevGain / (count - 1);
        for (int i = 0; i < count; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 45.0 + i * 0.001,
                Lon = 6.0,
                Ele = 500 + i * segElev,
                DistFromPrev = i == 0 ? 0 : segDist,
                Time = DateTime.UtcNow.AddSeconds(i * 120),
            });
        }
        return points;
    }

    // ── #103: grade floor and distance-weighted average ──

    // This fixture spaces points 3 m apart, below MinGradeSegmentM. Short segments
    // are accumulated to a 5 m baseline rather than discarded, so the 0.3 m jitter
    // segment is absorbed into a window long enough to make its 0.5 m residual an
    // ordinary grade instead of 167% - and the run is still measured, not skipped.
    [Fact]
    public void ComputeTerrainDifficulty_FlatRunWithASubMetreJitterSegment_DoesNotReportAnImpossibleGrade()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();

        // 1 Hz flat road run: ~3 m per second, elevation noise of +/- 0.3 m
        for (int i = 0; i < 600; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.000027,
                Lon = 2.0,
                Ele = 35 + (i % 3) * 0.3,
                Time = t0.AddSeconds(i),
                DistFromPrev = i == 0 ? 0 : 3.0,
            });

        // At a traffic light two consecutive samples are 0.3 m apart with a
        // residual 0.5 m elevation difference -> grade = 167%.
        points[300].DistFromPrev = 0.3;
        points[300].Ele = points[299].Ele + 0.5;

        var s = new Summary
        {
            TotalDistance = 1800,
            TotalTime = TimeSpan.FromSeconds(600),
            MovingTime = TimeSpan.FromSeconds(600),
        };

        var effort = EffortCalculator.ComputeAll(points, s);

        Assert.True(effort.TerrainDifficulty.MaxGradePercent < 30,
            $"a flat road run reported max_grade_percent = {effort.TerrainDifficulty.MaxGradePercent:F1}");
        Assert.Equal("Easy", effort.TerrainDifficulty.Grade);

        // Pin the NON-degenerate path. If short segments are ever discarded again
        // instead of accumulated, every segment of this 1 Hz fixture drops out and
        // max_grade_percent silently becomes 0 - which would satisfy both
        // assertions above while measuring nothing at all.
        Assert.True(effort.TerrainDifficulty.MaxGradePercent > 0,
            "max_grade_percent is 0: no segment cleared the baseline, so the run was not measured");
    }

    // Companion to the test above, with segments comfortably above the baseline:
    // the jitter segment must not put a 167% quotient into max_grade_percent, and
    // the real 5% terrain must still be reported.
    [Fact]
    public void ComputeTerrainDifficulty_JitterSegmentAmongRealSegments_AbsorbsItIntoTheNextWindow()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();

        // 600 points, 10 m apart, climbing a steady 0.5 m per segment -> 5% grade.
        for (int i = 0; i < 600; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.00009,
                Lon = 2.0,
                Ele = 35 + i * 0.5,
                Time = t0.AddSeconds(i * 3),
                DistFromPrev = i == 0 ? 0 : 10.0,
            });

        // One jitter segment: the same 0.5 m rise recorded over 0.3 m -> 167%.
        points[300].DistFromPrev = 0.3;

        var s = new Summary
        {
            TotalDistance = 5990,
            TotalTime = TimeSpan.FromSeconds(1797),
            MovingTime = TimeSpan.FromSeconds(1797),
            Elevation = new ElevationResult { Gain = 299.5, Loss = 0, Max = 334.5, Min = 35 },
        };

        var effort = EffortCalculator.ComputeAll(points, s);

        // The 167% quotient must not survive. The 0.3 m segment does not reach the
        // 5 m baseline on its own, so it is carried into the next one: its 0.5 m rise
        // plus the next segment 0.5 m over 0.3 + 10 m = 9.7%, not 166.7%. Absorbing
        // it keeps the elevation change the discarding version threw away.
        Assert.Equal(9.7, effort.TerrainDifficulty.MaxGradePercent, 1);

        // The real terrain still dominates the distance-weighted average.
        Assert.Equal(5.0, effort.TerrainDifficulty.AvgGradePercent, 1);
        Assert.Equal("Easy", effort.TerrainDifficulty.Grade);
    }

    // #103, second half: the average must be distance-weighted, so a burst of
    // near-stationary samples cannot outvote the segments carrying the terrain.
    [Fact]
    public void ComputeTerrainDifficulty_ManyShortSegments_DoNotOutvoteTheLongOnes()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint> { new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0 } };

        // 100 short-but-countable 6 m segments on flat ground (0% grade)...
        for (int i = 1; i <= 100; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0, Lon = 2.0, Ele = 100,
                Time = t0.AddSeconds(i * 6), DistFromPrev = 6.0,
            });

        // ...then 10 long 200 m segments climbing 20 m each (10% grade).
        var last = points[^1];
        for (int i = 1; i <= 10; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0, Lon = 2.0, Ele = 100 + i * 20,
                Time = last.Time.AddSeconds(i * 120), DistFromPrev = 200.0,
            });

        var s = new Summary
        {
            TotalDistance = 100 * 6 + 10 * 200,
            TotalTime = TimeSpan.FromSeconds(1800),
            MovingTime = TimeSpan.FromSeconds(1800),
            Elevation = new ElevationResult { Gain = 200, Loss = 0, Max = 300, Min = 100 },
        };

        var effort = EffortCalculator.ComputeAll(points, s);

        // Unweighted: 10 * 10% / 110 segments = 0.9%.
        // Distance-weighted: 2000 m at 10% over 2600 m total = 7.7%.
        Assert.Equal(7.7, effort.TerrainDifficulty.AvgGradePercent, 1);
    }

    // ── #103 follow-up: the grade floor must not reach the distance-consuming metrics ──

    /// <summary>
    /// A 1 Hz recording — the default cadence on most GPS watches — of a runner at
    /// 3 m/s climbing a steady 5%. Every segment is ~3 m, i.e. BELOW MinGradeSegmentM.
    ///
    /// No other fixture in this suite, and no golden, spaces points closer than 100 m,
    /// which is exactly why applying the grade floor to ToblerTime and
    /// EquivalentFlatDistance went unnoticed: it zeroed both metrics outright for the
    /// most common recording cadence there is.
    /// </summary>
    private static (List<TrackPoint> Points, Summary Summary) OneHertzClimb(
        double risePerPoint = 0.15, int count = 600)
    {
        var t0 = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<TrackPoint>();
        for (int i = 0; i < count; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * (3.0 / 111_320.0),   // 3 m north per second
                Lon = 2.0,
                Ele = 100 + i * risePerPoint,         // 0.15 m per 3 m = a steady 5%
                Time = t0.AddSeconds(i),
            });

        // Let the pipeline compute DistFromPrev, so the fixture proves that a 1 Hz
        // recording really does produce sub-5 m segments rather than asserting it.
        SpeedCalculator.EnrichPoints(points);

        var distM = points.Sum(p => p.DistFromPrev);
        var gain = (count - 1) * risePerPoint;
        return (points, new Summary
        {
            TotalDistance = distM,
            TotalTime = TimeSpan.FromSeconds(count - 1),
            MovingTime = TimeSpan.FromSeconds(count - 1),
            Elevation = new ElevationResult { Gain = gain, Loss = 0, Max = 100 + gain, Min = 100 },
        });
    }

    [Fact]
    public void OneHertzRecording_ProducesSegmentsBelowTheGradeFloor()
    {
        var (points, _) = OneHertzClimb();

        // The premise of every assertion below: this is what 1 Hz looks like.
        Assert.All(points.Skip(1), p => Assert.InRange(p.DistFromPrev, 2.5, 3.5));
    }

    [Fact]
    public void ToblerTime_OneHertzRecording_IsNotZeroedByTheGradeFloor()
    {
        var (points, summary) = OneHertzClimb();

        var effort = EffortCalculator.ComputeAll(points, summary);

        // Tobler at a 5% grade predicts 6*exp(-3.5*0.10) = 4.23 km/h, so ~1.8 km
        // takes ~25 min. A floor that skips every segment reports 0 instead.
        Assert.True(effort.ToblerTime > TimeSpan.Zero,
            "tobler_time is zero for a 1 Hz recording: every segment was skipped");
        Assert.InRange(effort.ToblerTime, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(32));

        // ...and the ratio derived from it must therefore also be real.
        Assert.True(effort.PerformanceRatioTobler > 0,
            "performance_ratio_tobler is zero because tobler_time was zero");
    }

    [Fact]
    public void EquivalentFlatDistance_OneHertzRecording_IsNotZeroedByTheGradeFloor()
    {
        var (points, summary) = OneHertzClimb();

        var effort = EffortCalculator.ComputeAll(points, summary);

        Assert.True(effort.EquivalentFlatDistanceKm > 0,
            "equivalent_flat_distance_km is zero for a 1 Hz recording: every segment was skipped");

        // Minetti's cost ratio at 5% is ~1.30, so ~1.8 km of climb is worth ~2.3 km flat.
        // It must exceed the raw distance (uphill costs more) but stay in the same order.
        var flatKm = summary.TotalDistance / 1000.0;
        Assert.True(effort.EquivalentFlatDistanceKm > flatKm,
            $"a 5% climb should cost more than its {flatKm:F2} km flat equivalent, " +
            $"got {effort.EquivalentFlatDistanceKm}");
        Assert.InRange(effort.EquivalentFlatDistanceKm, 2.0, 2.8);
    }

    // ── #140: short segments are accumulated to a baseline, not discarded ──

    [Fact]
    public void TerrainDifficulty_OneHertzSustainedTwentyPercentClimb_IsNotReportedAsEasy()
    {
        // 0.6 m of rise per 3 m of ground = a sustained 20% climb, 359 m of gain
        // over 1.8 km. Every segment is ~3 m, below MinGradeSegmentM.
        var (points, summary) = OneHertzClimb(risePerPoint: 0.6);

        var terrain = EffortCalculator.ComputeAll(points, summary).TerrainDifficulty;

        Assert.NotEqual("Easy", terrain.Grade);
        Assert.True(terrain.Score >= 5,
            $"a sustained 20% climb scored {terrain.Score} ({terrain.Grade})");

        // The grade itself must be measured, not zeroed.
        Assert.InRange(terrain.MaxGradePercent, 18, 22);
        Assert.InRange(terrain.AvgGradePercent, 18, 22);

        // 20% throughout, so every metre is a "steep section".
        Assert.Equal(1.0, terrain.SteepSectionRatio, 2);
    }

    // Asserted on its own, with no reference to any grade outcome: elevation_per_km
    // comes from the summary's own gain and distance and must stay correct whatever
    // policy the per-segment grades follow.
    [Fact]
    public void TerrainDifficulty_OneHertzRecording_ReportsElevationPerKm()
    {
        var (points, summary) = OneHertzClimb(risePerPoint: 0.6);

        var terrain = EffortCalculator.ComputeAll(points, summary).TerrainDifficulty;

        // 359.4 m of gain over 1.7973 km.
        var expected = summary.Elevation.Gain / (summary.TotalDistance / 1000.0);
        Assert.Equal(expected, terrain.ElevationPerKm, 1);
        Assert.InRange(terrain.ElevationPerKm, 195, 205);
    }

    // The same field on the path where no window clears the baseline at all — the
    // branch that used to discard it. Every segment here has zero distance (a fully
    // clamped track), so no grade can be computed, yet the climb is still 200 m/km.
    [Fact]
    public void TerrainDifficulty_NoMeasurableSegment_StillReportsElevationPerKm()
    {
        var t0 = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var points = new List<TrackPoint>();
        for (int i = 0; i < 100; i++)
            points.Add(new TrackPoint
            {
                Lat = 48.0, Lon = 2.0, Ele = 100 + i * 2,
                Time = t0.AddSeconds(i), DistFromPrev = 0,
            });

        var terrain = EffortCalculator.ComputeTerrainDifficulty(points, distanceM: 1000, elevGainM: 200);

        Assert.Equal(200.0, terrain.ElevationPerKm, 1);
        // No grade information exists here, so the score stays at its floor.
        Assert.Equal(0, terrain.MaxGradePercent);
    }
}
