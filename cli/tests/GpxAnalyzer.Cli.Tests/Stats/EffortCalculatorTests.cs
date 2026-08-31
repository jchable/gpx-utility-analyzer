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

    // NOTE: this fixture spaces points 3 m apart, which is below MinGradeSegmentM,
    // so after the fix EVERY segment is excluded and the difficulty falls back to
    // its "no measurable grade" branch. It still fails before the fix (the 0.3 m
    // jitter segment yields a 167% grade), but see the companion test below for
    // the case that proves real terrain is still measured.
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
    }

    // Companion to the test above: with segments comfortably above the grade
    // floor, the sub-metre jitter segment must be the ONLY thing dropped — the
    // real 5% terrain still has to be reported, so the floor cannot degenerate
    // into "measure nothing".
    [Fact]
    public void ComputeTerrainDifficulty_JitterSegmentAmongRealSegments_DropsOnlyTheJitter()
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

        // The jitter quotient must not survive into max_grade_percent...
        Assert.Equal(5.0, effort.TerrainDifficulty.MaxGradePercent, 1);
        // ...and the real terrain must still be measured.
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
}
