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
    [InlineData("running", 0.3, 300, 150)]
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
}
