using GpxAnalyzer.Cli.Gpx;
using GpxAnalyzer.Cli.Stats;

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
    public void Presets_ContainAllThree()
    {
        Assert.True(StopDetector.Presets.ContainsKey("hiking"));
        Assert.True(StopDetector.Presets.ContainsKey("trail"));
        Assert.True(StopDetector.Presets.ContainsKey("cycling"));
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
