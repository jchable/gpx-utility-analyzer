using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class BiometricsCalculatorTests
{
    [Fact]
    public void Compute_WithHeartRate_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { HeartRate = 120, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { HeartRate = 140, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
            new() { HeartRate = 160, Time = DateTime.Parse("2024-01-01T10:02:00Z").ToUniversalTime() },
            new() { HeartRate = 150, Time = DateTime.Parse("2024-01-01T10:03:00Z").ToUniversalTime() },
        };
        var cfg = new BiometricsConfig { MaxHR = 190 };
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.HeartRate);
        Assert.Equal(160, result.HeartRate!.Max);
        Assert.Equal(120, result.HeartRate.Min);
        Assert.True(result.HeartRate.Avg > 0);
        Assert.Equal(5, result.HeartRate.Zones.Count);
    }

    [Fact]
    public void Compute_NoHeartRate_ReturnsNull()
    {
        var points = new List<TrackPoint>
        {
            new() { Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
        };
        var cfg = new BiometricsConfig { MaxHR = 190 };
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.Null(result.HeartRate);
    }

    [Fact]
    public void Compute_WithPower_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { Power = 200, Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime() },
            new() { Power = 220, Time = DateTime.Parse("2024-01-01T10:01:00Z").ToUniversalTime() },
            new() { Power = 250, Time = DateTime.Parse("2024-01-01T10:02:00Z").ToUniversalTime() },
        };
        var cfg = new BiometricsConfig();
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.Power);
        Assert.Equal(250, result.Power!.Max);
        Assert.True(result.Power.Avg > 0);
        Assert.True(result.Power.NormalizedPower >= 0);
    }

    [Fact]
    public void Compute_WithCadence_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { Cadence = 80 },
            new() { Cadence = 90 },
            new() { Cadence = 85 },
        };
        var cfg = new BiometricsConfig();
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.Cadence);
        Assert.Equal(90, result.Cadence!.Max);
        Assert.True(result.Cadence.Avg > 0);
    }

    [Fact]
    public void Compute_WithTemperature_ReturnsResult()
    {
        var points = new List<TrackPoint>
        {
            new() { Temperature = 18.0 },
            new() { Temperature = 20.0 },
            new() { Temperature = 22.0 },
        };
        var cfg = new BiometricsConfig();
        var result = BiometricsCalculator.Compute(points, cfg);
        Assert.NotNull(result.Temperature);
        Assert.Equal(22.0, result.Temperature!.Max, 0.1);
        Assert.Equal(18.0, result.Temperature.Min, 0.1);
    }

    // #102 — ComputeHRZones credited the whole inter-sample interval to whichever
    // zone the LATER sample fell in, with no upper bound, so a recording gap was
    // charged in full to a single post-gap reading.
    [Fact]
    public void ComputeHRZones_RecordingGap_DoesNotCreditTheGapToAZone()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();

        // 30 min of riding at 120 bpm, one sample every 10 s
        for (int i = 0; i < 180; i++)
            points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = t0.AddSeconds(i * 10), HeartRate = 120 });

        // 25 min tunnel: no samples. First sample after it reads 145 bpm.
        var resume = points[^1].Time.AddMinutes(25);
        points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = resume, HeartRate = 145 });
        for (int i = 1; i < 180; i++)
            points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = resume.AddSeconds(i * 10), HeartRate = 120 });

        var result = BiometricsCalculator.Compute(points, new BiometricsConfig { MaxHR = 190 });

        Assert.NotNull(result.HeartRate);
        var zoneTotal = result.HeartRate!.Zones.Aggregate(TimeSpan.Zero, (a, z) => a + z.Duration);
        var elapsed = points[^1].Time - points[0].Time;

        // The gap must not be attributed to any zone, so the zones cannot sum
        // to more than the recorded time minus the gap.
        Assert.True(zoneTotal <= elapsed - TimeSpan.FromMinutes(20),
            $"zone durations sum to {zoneTotal} over an elapsed {elapsed} that includes a 25 min gap");
    }

    // #102 negative control: an ordinary short interval must still be credited,
    // so capping cannot degenerate into dropping every sample.
    [Fact]
    public void ComputeHRZones_NoGap_StillCreditsTheWholeSession()
    {
        var t0 = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int i = 0; i < 180; i++)
            points.Add(new TrackPoint { Lat = 48.0, Lon = 2.0, Time = t0.AddSeconds(i * 10), HeartRate = 120 });

        var result = BiometricsCalculator.Compute(points, new BiometricsConfig { MaxHR = 190 });

        Assert.NotNull(result.HeartRate);
        var zoneTotal = result.HeartRate!.Zones.Aggregate(TimeSpan.Zero, (a, z) => a + z.Duration);
        Assert.Equal(points[^1].Time - points[0].Time, zoneTotal);

        // 120/190 = 63% -> Z2 (Endurance)
        Assert.Equal(points[^1].Time - points[0].Time, result.HeartRate.Zones[1].Duration);
    }
}
