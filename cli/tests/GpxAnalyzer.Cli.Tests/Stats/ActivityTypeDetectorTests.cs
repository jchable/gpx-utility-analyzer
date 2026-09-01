using GpxAnalyzer.Cli.Core.Stats;
using GpxAiAnalyzer.Core.Models;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class ActivityTypeDetectorTests
{
    #region Phase A: DetectFromGpxType

    [Theory]
    [InlineData("running", "run")]
    [InlineData("run", "run")]
    [InlineData("trail_running", "trail")]
    [InlineData("trail running", "trail")]
    [InlineData("trailrun", "trail")]
    [InlineData("hiking", "hike")]
    [InlineData("hike", "hike")]
    [InlineData("cycling", "cycle")]
    [InlineData("biking", "cycle")]
    [InlineData("ride", "cycle")]
    [InlineData("road_biking", "cycle")]
    [InlineData("mountain_biking", "cycle")]
    [InlineData("walking", "walk")]
    [InlineData("walk", "walk")]
    [InlineData("swimming", "swim")]
    [InlineData("swim", "swim")]
    [InlineData("lap_swimming", "swim")]
    [InlineData("open_water_swimming", "swim")]
    public void DetectFromGpxType_KnownTypes_ReturnsCorrectMapping(string gpxType, string expected)
    {
        Assert.Equal(expected, ActivityTypeDetector.DetectFromGpxType(gpxType));
    }

    [Theory]
    [InlineData("Running")]
    [InlineData("CYCLING")]
    [InlineData("  hiking  ")]
    public void DetectFromGpxType_CaseInsensitive_ReturnsCorrectMapping(string gpxType)
    {
        Assert.NotNull(ActivityTypeDetector.DetectFromGpxType(gpxType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown_sport")]
    [InlineData("skiing")]
    public void DetectFromGpxType_UnknownOrEmpty_ReturnsNull(string? gpxType)
    {
        Assert.Null(ActivityTypeDetector.DetectFromGpxType(gpxType));
    }

    #endregion

    #region Phase B: DetectFromStats

    private static GpxStats MakeStats(
        double avgMovingSpeedKmh,
        double elevationGainM = 0,
        double totalDistanceKm = 10,
        double maxElevationM = 100,
        double minElevationM = 50,
        double terrainScore = 0,
        PowerStats? power = null,
        List<StopInfo>? stops = null)
    {
        return new GpxStats
        {
            AvgMovingSpeedKmh = avgMovingSpeedKmh,
            ElevationGainM = elevationGainM,
            TotalDistanceKm = totalDistanceKm,
            MaxElevationM = maxElevationM,
            MinElevationM = minElevationM,
            Power = power,
            Stops = stops,
            Effort = new EffortStatsModel
            {
                TerrainDifficulty = new TerrainDifficultyModel { Score = terrainScore }
            },
        };
    }

    [Fact]
    public void DetectFromStats_FastCycling_ReturnsCycle()
    {
        var stats = MakeStats(avgMovingSpeedKmh: 22.0, elevationGainM: 300, totalDistanceKm: 50);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("cycle", result.ActivityType);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void DetectFromStats_ModerateSpeedWithPower_ReturnsCycle()
    {
        var stats = MakeStats(avgMovingSpeedKmh: 13.0, power: new PowerStats { AvgWatts = 200 });
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("cycle", result.ActivityType);
    }

    [Fact]
    public void DetectFromStats_FlatRunning_ReturnsRun()
    {
        var stats = MakeStats(avgMovingSpeedKmh: 10.0, elevationGainM: 50, totalDistanceKm: 10);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("run", result.ActivityType);
        Assert.True(result.Confidence >= 0.8);
    }

    [Fact]
    public void DetectFromStats_MountainTrail_ReturnsTrail()
    {
        var stats = MakeStats(avgMovingSpeedKmh: 8.0, elevationGainM: 600, totalDistanceKm: 12, terrainScore: 5);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("trail", result.ActivityType);
    }

    [Fact]
    public void DetectFromStats_SlowWithElevation_ReturnsHike()
    {
        var stats = MakeStats(avgMovingSpeedKmh: 4.0, elevationGainM: 400, totalDistanceKm: 12);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("hike", result.ActivityType);
    }

    [Fact]
    public void DetectFromStats_SlowFlat_ReturnsWalk()
    {
        var stats = MakeStats(avgMovingSpeedKmh: 4.5, elevationGainM: 30, totalDistanceKm: 5);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("walk", result.ActivityType);
    }

    [Fact]
    public void DetectFromStats_VerySlowNoElevation_ReturnsSwim()
    {
        var stats = MakeStats(
            avgMovingSpeedKmh: 3.0,
            elevationGainM: 5,
            totalDistanceKm: 2,
            maxElevationM: 2,
            minElevationM: 0);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("swim", result.ActivityType);
    }

    [Fact]
    public void DetectFromStats_VerySlowWithElevation_ReturnsHikeNotSwim()
    {
        var stats = MakeStats(
            avgMovingSpeedKmh: 3.0,
            elevationGainM: 200,
            totalDistanceKm: 5,
            maxElevationM: 800,
            minElevationM: 500);
        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("hike", result.ActivityType);
    }

    #endregion

    #region Backyard detection

    [Fact]
    public void DetectFromStats_BackyardPattern_ReturnsRunWithBackyardSubType()
    {
        var baseTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var stops = new List<StopInfo>();
        // 5 stops, each ~20 min, at 60-min intervals
        for (int i = 0; i < 5; i++)
        {
            var stopStart = baseTime.AddMinutes(40 + i * 60); // stop after 40 min of each lap
            stops.Add(new StopInfo
            {
                StartTime = stopStart.ToString("O"),
                EndTime = stopStart.AddMinutes(20).ToString("O"),
                Duration = new DurationValue { Seconds = 1200 },
            });
        }

        var stats = MakeStats(
            avgMovingSpeedKmh: 10.0,
            elevationGainM: 100,
            totalDistanceKm: 6.706 * 6, // 6 laps
            stops: stops);

        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("run", result.ActivityType);
        Assert.Equal("backyard", result.SubType);
    }

    [Fact]
    public void DetectFromStats_IrregularStops_NoBackyardSubType()
    {
        var baseTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var stops = new List<StopInfo>
        {
            new() { StartTime = baseTime.AddMinutes(30).ToString("O"), EndTime = baseTime.AddMinutes(40).ToString("O"), Duration = new DurationValue { Seconds = 600 } },
            new() { StartTime = baseTime.AddMinutes(120).ToString("O"), EndTime = baseTime.AddMinutes(130).ToString("O"), Duration = new DurationValue { Seconds = 600 } },
            new() { StartTime = baseTime.AddMinutes(140).ToString("O"), EndTime = baseTime.AddMinutes(150).ToString("O"), Duration = new DurationValue { Seconds = 600 } },
        };

        var stats = MakeStats(
            avgMovingSpeedKmh: 10.0,
            elevationGainM: 100,
            totalDistanceKm: 30,
            stops: stops);

        var result = ActivityTypeDetector.DetectFromStats(stats);
        Assert.Equal("run", result.ActivityType);
        Assert.Null(result.SubType);
    }

    // ── #101: the lap-distance tolerance was unreachable ──

    /// <summary>
    /// Builds a "run"-shaped GpxStats whose stop pattern clears every earlier
    /// backyard gate (>= 3 qualifying stops of 3-30 min, mean interval 50-70 min,
    /// CV &lt;= 0.15), so the only thing left to decide the sub-type is the
    /// lap-distance check.
    /// </summary>
    private static GpxStats BuildBackyardShapedStats(double totalDistanceKm, int stopIntervalMinutes)
    {
        var baseTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var stops = new List<StopInfo>();
        for (int i = 0; i < 5; i++)
        {
            var stopStart = baseTime.AddMinutes(40 + i * stopIntervalMinutes);
            stops.Add(new StopInfo
            {
                StartTime = stopStart.ToString("O"),
                EndTime = stopStart.AddMinutes(10).ToString("O"),
                Duration = new DurationValue { Seconds = 600 },
            });
        }

        return MakeStats(
            avgMovingSpeedKmh: 10.0,   // run branch: 7-18 km/h
            elevationGainM: 100,       // elevPerKm well under 30
            totalDistanceKm: totalDistanceKm,
            stops: stops);
    }

    [Fact]
    public void DetectFromStats_IntervalWorkoutOnAnHourlyCadence_IsNotLabelledBackyard()
    {
        // 30 km at 10 km/h with rests spaced almost exactly 60 min apart: a common
        // interval session. 30 / 6.706 = 4.474 laps, which is nowhere near a whole
        // number of backyard laps.
        var stats = BuildBackyardShapedStats(totalDistanceKm: 30.0, stopIntervalMinutes: 60);

        var detection = ActivityTypeDetector.DetectFromStats(stats);

        Assert.Equal("run", detection.ActivityType);
        Assert.NotEqual("backyard", detection.SubType);
    }

    [Fact]
    public void DetectFromStats_RealBackyardUltra_IsStillLabelled()
    {
        // 6 laps of 6.706 km = 40.236 km on an hourly cadence.
        var stats = BuildBackyardShapedStats(totalDistanceKm: 6 * 6.706, stopIntervalMinutes: 60);
        Assert.Equal("backyard", ActivityTypeDetector.DetectFromStats(stats).SubType);
    }

    #endregion
}
