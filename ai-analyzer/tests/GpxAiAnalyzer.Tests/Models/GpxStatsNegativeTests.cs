namespace GpxAiAnalyzer.Tests.Models;

using GpxAiAnalyzer.Core.Models;
using System.Text.Json;

public class GpxStatsNegativeTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_EmptyJson_ReturnsDefaults()
    {
        var stats = JsonSerializer.Deserialize<GpxStats>("{}", JsonOpts)!;
        Assert.Equal("", stats.Filename);
        Assert.Equal(0, stats.TotalDistanceM);
        Assert.Equal(0, stats.ElevationGainM);
        Assert.Null(stats.HeartRate);
        Assert.Null(stats.Power);
        Assert.Null(stats.Stops);
        Assert.Null(stats.LongestStop);
    }

    [Fact]
    public void Deserialize_MissingOptionalFields_NoException()
    {
        // Minimal JSON with only required-like fields
        const string json = """
        {
            "filename": "test.gpx",
            "total_distance_m": 1000.0,
            "total_distance_km": 1.0
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.Equal("test.gpx", stats.Filename);
        Assert.Equal(1000.0, stats.TotalDistanceM);
        Assert.Equal(0, stats.ElevationGainM);
        Assert.Equal(0, stats.StopCount);
        Assert.Null(stats.HeartRate);
    }

    [Fact]
    public void Deserialize_UnknownExtraFields_Ignored()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "total_distance_m": 500.0,
            "some_unknown_field": "should be ignored",
            "another_unknown": 42
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.Equal("test.gpx", stats.Filename);
        Assert.Equal(500.0, stats.TotalDistanceM);
    }

    [Fact]
    public void Deserialize_NullBiometrics_ReturnsNull()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "heart_rate": null,
            "power": null,
            "cadence": null,
            "temperature": null
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.Null(stats.HeartRate);
        Assert.Null(stats.Power);
        Assert.Null(stats.Cadence);
        Assert.Null(stats.Temperature);
    }

    [Fact]
    public void Deserialize_EmptyStopsArray_ReturnsEmptyList()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "stops": [],
            "stop_count": 0
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.NotNull(stats.Stops);
        Assert.Empty(stats.Stops);
        Assert.Equal(0, stats.StopCount);
    }

    [Fact]
    public void Deserialize_DurationValue_HasDisplayAndSeconds()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "total_time": {
                "display": "2h 30m 0s",
                "seconds": 9000.0
            }
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.Equal("2h 30m 0s", stats.TotalTime.Display);
        Assert.Equal(9000.0, stats.TotalTime.Seconds);
    }

    [Fact]
    public void Deserialize_StopInfo_HasAllFields()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "longest_stop": {
                "start_time": "2024-06-15T09:30:00Z",
                "end_time": "2024-06-15T10:00:00Z",
                "duration": { "display": "30m 0s", "seconds": 1800.0 },
                "lat": 45.92,
                "lon": 6.87
            }
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.NotNull(stats.LongestStop);
        Assert.Equal(1800.0, stats.LongestStop.Duration.Seconds);
        Assert.Equal(45.92, stats.LongestStop.Lat);
        Assert.Equal(6.87, stats.LongestStop.Lon);
        Assert.Contains("2024-06-15", stats.LongestStop.StartTime);
    }

    [Fact]
    public void Deserialize_HeartRateZones_EmptyArray()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "heart_rate": {
                "avg_bpm": 140.0,
                "max_bpm": 180,
                "min_bpm": 90,
                "zones": []
            }
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.NotNull(stats.HeartRate);
        Assert.Equal(140.0, stats.HeartRate.AvgBpm);
        Assert.NotNull(stats.HeartRate.Zones);
        Assert.Empty(stats.HeartRate.Zones);
    }

    [Fact]
    public void Deserialize_HeartRateZones_Null()
    {
        const string json = """
        {
            "filename": "test.gpx",
            "heart_rate": {
                "avg_bpm": 140.0,
                "max_bpm": 180,
                "min_bpm": 90,
                "zones": null
            }
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;
        Assert.NotNull(stats.HeartRate);
        Assert.Null(stats.HeartRate.Zones);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<GpxStats>("not json at all", JsonOpts));
    }

    [Fact]
    public void Deserialize_TypeMismatch_ThrowsJsonException()
    {
        // total_distance_m should be a number, not a string
        const string json = """
        {
            "total_distance_m": "not a number"
        }
        """;
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<GpxStats>(json, JsonOpts));
    }

    [Fact]
    public void Deserialize_AllFieldsPresent_ContractVerification()
    {
        // Verify the full JSON contract between Go CLI and .NET
        const string json = """
        {
            "filename": "test.gpx",
            "total_distance_m": 10234.5,
            "total_distance_3d_m": 10456.2,
            "total_distance_km": 10.2345,
            "elevation_gain_m": 850.0,
            "elevation_loss_m": 720.0,
            "max_elevation_m": 1450.0,
            "min_elevation_m": 620.0,
            "start_time": "2024-06-15T08:00:00Z",
            "end_time": "2024-06-15T11:30:00Z",
            "total_time": { "display": "3h 30m 0s", "seconds": 12600.0 },
            "moving_time": { "display": "3h 0m 0s", "seconds": 10800.0 },
            "stopped_time": { "display": "30m 0s", "seconds": 1800.0 },
            "avg_speed_kmh": 2.92,
            "avg_moving_speed_kmh": 3.41,
            "max_speed_kmh": 12.6,
            "avg_pace": "20:31 min/km",
            "avg_moving_pace": "17:35 min/km",
            "point_count": 2500,
            "segment_count": 1,
            "points_per_km": 244.3,
            "stop_count": 1,
            "total_stop_time": { "display": "30m 0s", "seconds": 1800.0 },
            "avg_stop_duration": { "display": "30m 0s", "seconds": 1800.0 },
            "longest_stop": {
                "start_time": "2024-06-15T09:30:00Z",
                "end_time": "2024-06-15T10:00:00Z",
                "duration": { "display": "30m 0s", "seconds": 1800.0 },
                "lat": 45.92,
                "lon": 6.87
            },
            "stops": [{
                "start_time": "2024-06-15T09:30:00Z",
                "end_time": "2024-06-15T10:00:00Z",
                "duration": { "display": "30m 0s", "seconds": 1800.0 },
                "lat": 45.92,
                "lon": 6.87
            }]
        }
        """;
        var stats = JsonSerializer.Deserialize<GpxStats>(json, JsonOpts)!;

        // Distance
        Assert.Equal("test.gpx", stats.Filename);
        Assert.Equal(10234.5, stats.TotalDistanceM);
        Assert.Equal(10456.2, stats.TotalDistance3dM);
        Assert.Equal(10.2345, stats.TotalDistanceKm);

        // Elevation
        Assert.Equal(850.0, stats.ElevationGainM);
        Assert.Equal(720.0, stats.ElevationLossM);
        Assert.Equal(1450.0, stats.MaxElevationM);
        Assert.Equal(620.0, stats.MinElevationM);

        // Time
        Assert.Equal("2024-06-15T08:00:00Z", stats.StartTime);
        Assert.Equal(12600.0, stats.TotalTime.Seconds);
        Assert.Equal("3h 30m 0s", stats.TotalTime.Display);
        Assert.Equal(10800.0, stats.MovingTime.Seconds);
        Assert.Equal(1800.0, stats.StoppedTime.Seconds);

        // Speed
        Assert.Equal(2.92, stats.AvgSpeedKmh);
        Assert.Equal(3.41, stats.AvgMovingSpeedKmh);
        Assert.Equal(12.6, stats.MaxSpeedKmh);
        Assert.Equal("20:31 min/km", stats.AvgPace);
        Assert.Equal("17:35 min/km", stats.AvgMovingPace);

        // Points
        Assert.Equal(2500, stats.PointCount);
        Assert.Equal(1, stats.SegmentCount);
        Assert.Equal(244.3, stats.PointsPerKm);

        // Stops
        Assert.Equal(1, stats.StopCount);
        Assert.NotNull(stats.Stops);
        Assert.Single(stats.Stops);
        Assert.NotNull(stats.LongestStop);
        Assert.Equal(1800.0, stats.LongestStop.Duration.Seconds);
        Assert.Equal(45.92, stats.LongestStop.Lat);
        Assert.Equal(6.87, stats.LongestStop.Lon);
    }
}
