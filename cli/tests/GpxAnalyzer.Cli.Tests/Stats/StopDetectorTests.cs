using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class StopDetectorTests
{
    [Fact]
    public void DetectStops_NoStops_ReturnsEmpty()
    {
        // Points moving at constant speed ~1 m/s
        var points = new List<TrackPoint>();
        for (int i = 0; i < 10; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.00001,
                Lon = 2.0 + i * 0.00001,
                CalcSpeed = 1.5,
                Time = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddMinutes(i),
            });
        }
        var cfg = StopDetector.Presets["hiking"];
        var stops = StopDetector.DetectStops(points, cfg);
        Assert.Empty(stops);
    }

    [Fact]
    public void DetectStops_WithStop_DetectsOne()
    {
        var points = new List<TrackPoint>();
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Moving points
        for (int i = 0; i < 5; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.001,
                Lon = 2.0 + i * 0.001,
                CalcSpeed = 1.5,
                Time = baseTime.AddMinutes(i),
            });
        }

        // Stopped points (same location, speed = 0, 5 minutes = 300s)
        for (int i = 0; i < 6; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.004,
                Lon = 2.004,
                CalcSpeed = 0.05,
                Time = baseTime.AddMinutes(5 + i),
            });
        }

        // Resume moving
        for (int i = 0; i < 3; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.004 + (i + 1) * 0.001,
                Lon = 2.004 + (i + 1) * 0.001,
                CalcSpeed = 1.5,
                Time = baseTime.AddMinutes(11 + i),
            });
        }

        var cfg = StopDetector.Presets["hiking"]; // 0.2 m/s, 3 min, 30m
        var stops = StopDetector.DetectStops(points, cfg);
        Assert.True(stops.Count >= 1);
    }

    [Fact]
    public void Presets_ContainAllSix()
    {
        Assert.True(StopDetector.Presets.ContainsKey("hiking"));
        Assert.True(StopDetector.Presets.ContainsKey("trail"));
        Assert.True(StopDetector.Presets.ContainsKey("cycling"));
        Assert.True(StopDetector.Presets.ContainsKey("running"));
        Assert.True(StopDetector.Presets.ContainsKey("swimming"));
        Assert.True(StopDetector.Presets.ContainsKey("walking"));
    }

    [Theory]
    [InlineData("hiking", 0.2, 180, 30)]
    [InlineData("trail", 0.3, 120, 50)]
    [InlineData("cycling", 1.0, 30, 100)]
    [InlineData("running", 0.5, 300, 150)]
    [InlineData("swimming", 0.15, 120, 100)]
    [InlineData("walking", 0.2, 180, 30)]
    public void Presets_HaveExpectedValues(string preset, double maxSpeed, double minDurationSec, double maxDistance)
    {
        var cfg = StopDetector.Presets[preset];
        Assert.Equal(maxSpeed, cfg.MaxSpeed);
        Assert.Equal(TimeSpan.FromSeconds(minDurationSec), cfg.MinDuration);
        Assert.Equal(maxDistance, cfg.MaxDistance);
    }

    [Fact]
    public void DetectStops_RunningPreset_IgnoresShortPauses()
    {
        var points = new List<TrackPoint>();
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Moving
        for (int i = 0; i < 5; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.001, Lon = 2.0, CalcSpeed = 3.0, Time = baseTime.AddMinutes(i) });

        // Short pause (3 min) — should NOT be detected with running preset (MinDuration=5min)
        for (int i = 0; i < 4; i++)
            points.Add(new TrackPoint { Lat = 48.004, Lon = 2.0, CalcSpeed = 0.05, Time = baseTime.AddMinutes(5 + i) });

        // Resume
        for (int i = 0; i < 3; i++)
            points.Add(new TrackPoint { Lat = 48.004 + (i + 1) * 0.001, Lon = 2.0, CalcSpeed = 3.0, Time = baseTime.AddMinutes(9 + i) });

        var cfg = StopDetector.Presets["running"];
        var stops = StopDetector.DetectStops(points, cfg);
        Assert.Empty(stops); // 3 min pause is below running's 5 min threshold
    }

    [Fact]
    public void DetectStops_GracePeriod_BridgesShortSpike()
    {
        var points = new List<TrackPoint>();
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Moving (3 points)
        for (int i = 0; i < 3; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.001, Lon = 2.0, CalcSpeed = 3.0, Time = baseTime.AddSeconds(i * 10) });

        // Stopped for 5 min (every 10s = 30 points)
        for (int i = 0; i < 30; i++)
            points.Add(new TrackPoint { Lat = 48.003, Lon = 2.0, CalcSpeed = 0.05, Time = baseTime.AddSeconds(30 + i * 10) });

        // GPS spike for 15s (2 points within 30s grace — last slow was at 30+290=320s)
        points.Add(new TrackPoint { Lat = 48.003, Lon = 2.0, CalcSpeed = 0.5, Time = baseTime.AddSeconds(335) });
        points.Add(new TrackPoint { Lat = 48.003, Lon = 2.0, CalcSpeed = 0.5, Time = baseTime.AddSeconds(345) });

        // Stopped again for 5 min (every 10s = 30 points)
        for (int i = 0; i < 30; i++)
            points.Add(new TrackPoint { Lat = 48.003, Lon = 2.0, CalcSpeed = 0.05, Time = baseTime.AddSeconds(350 + i * 10) });

        // Resume moving
        for (int i = 0; i < 3; i++)
            points.Add(new TrackPoint { Lat = 48.003 + (i + 1) * 0.001, Lon = 2.0, CalcSpeed = 3.0, Time = baseTime.AddSeconds(650 + i * 10) });

        var cfg = new StopConfig { MaxSpeed = 0.3, MinDuration = TimeSpan.FromMinutes(5), MaxDistance = 150, GracePeriod = TimeSpan.FromSeconds(30) };
        var stops = StopDetector.DetectStops(points, cfg);

        // Should detect ONE stop (spike bridged by grace period), spanning ~10 minutes
        Assert.Single(stops);
        Assert.True(stops[0].Duration >= TimeSpan.FromMinutes(8));
    }

    [Fact]
    public void DetectStops_GracePeriod_SplitsOnLongGap()
    {
        var points = new List<TrackPoint>();
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Moving
        for (int i = 0; i < 3; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.001, Lon = 2.0, CalcSpeed = 3.0, Time = baseTime.AddMinutes(i) });

        // Stopped for 4 min
        for (int i = 0; i < 5; i++)
            points.Add(new TrackPoint { Lat = 48.003, Lon = 2.0, CalcSpeed = 0.05, Time = baseTime.AddMinutes(3 + i) });

        // Fast movement for 60s (exceeds 30s grace period)
        points.Add(new TrackPoint { Lat = 48.003, Lon = 2.0, CalcSpeed = 2.0, Time = baseTime.AddMinutes(8) });
        points.Add(new TrackPoint { Lat = 48.004, Lon = 2.0, CalcSpeed = 2.0, Time = baseTime.AddMinutes(9) });

        // Stopped again for 4 min
        for (int i = 0; i < 5; i++)
            points.Add(new TrackPoint { Lat = 48.004, Lon = 2.0, CalcSpeed = 0.05, Time = baseTime.AddMinutes(10 + i) });

        // Resume moving
        for (int i = 0; i < 3; i++)
            points.Add(new TrackPoint { Lat = 48.004 + (i + 1) * 0.001, Lon = 2.0, CalcSpeed = 3.0, Time = baseTime.AddMinutes(15 + i) });

        var cfg = new StopConfig { MaxSpeed = 0.3, MinDuration = TimeSpan.FromMinutes(3), MaxDistance = 150, GracePeriod = TimeSpan.FromSeconds(30) };
        var stops = StopDetector.DetectStops(points, cfg);

        // Should detect TWO stops (gap > grace period)
        Assert.Equal(2, stops.Count);
    }

    [Fact]
    public void MergeStops_MergesNearbyStops()
    {
        var points = new List<TrackPoint>();
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Build points spanning 20 min
        for (int i = 0; i <= 20; i++)
            points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, CalcSpeed = 0, Time = baseTime.AddMinutes(i) });

        var stops = new List<Stop>
        {
            new() { StartTime = baseTime, EndTime = baseTime.AddMinutes(5), Duration = TimeSpan.FromMinutes(5), Lat = 48.0, Lon = 2.0 },
            new() { StartTime = baseTime.AddMinutes(6), EndTime = baseTime.AddMinutes(10), Duration = TimeSpan.FromMinutes(4), Lat = 48.0, Lon = 2.0 },
        };
        var cfg = new StopConfig { MaxSpeed = 0.3, MinDuration = TimeSpan.FromMinutes(3), MaxDistance = 150, MergeGap = TimeSpan.FromSeconds(90) };
        var merged = StopDetector.MergeStops(stops, points, cfg);

        Assert.Single(merged);
        Assert.Equal(baseTime, merged[0].StartTime);
        Assert.Equal(baseTime.AddMinutes(10), merged[0].EndTime);
        Assert.Equal(TimeSpan.FromMinutes(10), merged[0].Duration);
    }

    [Fact]
    public void MergeStops_KeepsDistantStopsSeparate()
    {
        var points = new List<TrackPoint>();
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i <= 20; i++)
            points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, CalcSpeed = 0, Time = baseTime.AddMinutes(i) });

        var stops = new List<Stop>
        {
            new() { StartTime = baseTime, EndTime = baseTime.AddMinutes(5), Duration = TimeSpan.FromMinutes(5), Lat = 48.0, Lon = 2.0 },
            new() { StartTime = baseTime.AddMinutes(8), EndTime = baseTime.AddMinutes(15), Duration = TimeSpan.FromMinutes(7), Lat = 48.0, Lon = 2.0 },
        };
        var cfg = new StopConfig { MaxSpeed = 0.3, MinDuration = TimeSpan.FromMinutes(3), MaxDistance = 150, MergeGap = TimeSpan.FromSeconds(90) };
        var merged = StopDetector.MergeStops(stops, points, cfg);

        // Gap = 3 min > 90s → stays separate
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Presets_HaveDefaultGracePeriodAndMergeGap()
    {
        foreach (var (name, cfg) in StopDetector.Presets)
        {
            Assert.Equal(TimeSpan.FromSeconds(30), cfg.GracePeriod);
            Assert.Equal(TimeSpan.FromSeconds(90), cfg.MergeGap);
        }
    }

    [Fact]
    public void TotalStopTime_WithStops_ReturnsCorrectDuration()
    {
        var stops = new List<Stop>
        {
            new() { Duration = TimeSpan.FromMinutes(5), StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(5), Lat = 0, Lon = 0 },
            new() { Duration = TimeSpan.FromMinutes(10), StartTime = DateTime.UtcNow.AddMinutes(15), EndTime = DateTime.UtcNow.AddMinutes(25), Lat = 0, Lon = 0 },
        };
        Assert.Equal(TimeSpan.FromMinutes(15), StopDetector.TotalStopTime(stops));
    }

    [Fact]
    public void LongestStop_ReturnsLongest()
    {
        var stops = new List<Stop>
        {
            new() { Duration = TimeSpan.FromMinutes(5), StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(5), Lat = 0, Lon = 0 },
            new() { Duration = TimeSpan.FromMinutes(10), StartTime = DateTime.UtcNow.AddMinutes(15), EndTime = DateTime.UtcNow.AddMinutes(25), Lat = 0, Lon = 0 },
        };
        var longest = StopDetector.LongestStop(stops);
        Assert.NotNull(longest);
        Assert.Equal(TimeSpan.FromMinutes(10), longest!.Duration);
    }

    [Fact]
    public void DetectStops_AutoPauseGap_CountsAsAStopEvenWhenTheUserMoved()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();

        // 10 min of hiking, one point every 30 s
        for (int i = 0; i < 20; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.0002, Lon = 2.0, Time = t0.AddSeconds(i * 30) });

        // Watch auto-pauses for 45 min; the hiker resumes ~50 m from where they stopped
        var resume = points[^1].Time.AddMinutes(45);
        double lastLat = points[^1].Lat;
        points.Add(new TrackPoint { Lat = lastLat + 0.00045, Lon = 2.0, Time = resume });

        // ...then hikes on for another 10 min
        for (int i = 1; i < 20; i++)
            points.Add(new TrackPoint
            {
                Lat = lastLat + 0.00045 + i * 0.0002, Lon = 2.0,
                Time = resume.AddSeconds(i * 30),
            });

        SpeedCalculator.EnrichPoints(points);
        var stops = StopDetector.DetectStops(points, StopDetector.Presets[StopDetector.PresetHiking]);

        Assert.NotEmpty(stops);
        var total = StopDetector.TotalStopTime(stops);
        Assert.True(total >= TimeSpan.FromMinutes(40),
            $"the 45 min auto-pause should be counted as stopped time, got {total}");
    }

    [Fact]
    public void DetectStops_RecordedStandstillBeyondMaxDistance_IsStillRejected()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();

        // 5 min of continuously recorded very slow movement covering ~200 m,
        // well past the hiking MaxDistance of 30 m: not a stop.
        for (int i = 0; i < 60; i++)
            points.Add(new TrackPoint { Lat = 48.0 + i * 0.00003, Lon = 2.0, Time = t0.AddSeconds(i * 5) });

        SpeedCalculator.EnrichPoints(points);
        var stops = StopDetector.DetectStops(points, StopDetector.Presets[StopDetector.PresetHiking]);

        Assert.Empty(stops);
    }
}
