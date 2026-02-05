namespace GpxAiAnalyzer.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Deserialization model matching the Go CLI JSON output exactly.
/// Contract defined in cli/internal/output/json.go (jsonSummary struct).
/// </summary>
public sealed class GpxStats
{
    [JsonPropertyName("filename")]
    public string Filename { get; init; } = "";

    // Distance
    [JsonPropertyName("total_distance_m")]
    public double TotalDistanceM { get; init; }

    [JsonPropertyName("total_distance_3d_m")]
    public double TotalDistance3dM { get; init; }

    [JsonPropertyName("total_distance_km")]
    public double TotalDistanceKm { get; init; }

    // Elevation
    [JsonPropertyName("elevation_gain_m")]
    public double ElevationGainM { get; init; }

    [JsonPropertyName("elevation_loss_m")]
    public double ElevationLossM { get; init; }

    [JsonPropertyName("max_elevation_m")]
    public double MaxElevationM { get; init; }

    [JsonPropertyName("min_elevation_m")]
    public double MinElevationM { get; init; }

    // Time
    [JsonPropertyName("start_time")]
    public string StartTime { get; init; } = "";

    [JsonPropertyName("end_time")]
    public string EndTime { get; init; } = "";

    [JsonPropertyName("total_time")]
    public DurationValue TotalTime { get; init; } = new();

    [JsonPropertyName("moving_time")]
    public DurationValue MovingTime { get; init; } = new();

    [JsonPropertyName("stopped_time")]
    public DurationValue StoppedTime { get; init; } = new();

    // Speed
    [JsonPropertyName("avg_speed_kmh")]
    public double AvgSpeedKmh { get; init; }

    [JsonPropertyName("avg_moving_speed_kmh")]
    public double AvgMovingSpeedKmh { get; init; }

    [JsonPropertyName("max_speed_kmh")]
    public double MaxSpeedKmh { get; init; }

    [JsonPropertyName("avg_pace")]
    public string AvgPace { get; init; } = "";

    [JsonPropertyName("avg_moving_pace")]
    public string AvgMovingPace { get; init; } = "";

    // Points
    [JsonPropertyName("point_count")]
    public int PointCount { get; init; }

    [JsonPropertyName("segment_count")]
    public int SegmentCount { get; init; }

    [JsonPropertyName("points_per_km")]
    public double PointsPerKm { get; init; }

    // Stops
    [JsonPropertyName("stop_count")]
    public int StopCount { get; init; }

    [JsonPropertyName("total_stop_time")]
    public DurationValue TotalStopTime { get; init; } = new();

    [JsonPropertyName("avg_stop_duration")]
    public DurationValue AvgStopDuration { get; init; } = new();

    [JsonPropertyName("longest_stop")]
    public StopInfo? LongestStop { get; init; }

    [JsonPropertyName("stops")]
    public List<StopInfo>? Stops { get; init; }
}

public sealed class DurationValue
{
    [JsonPropertyName("display")]
    public string Display { get; init; } = "";

    [JsonPropertyName("seconds")]
    public double Seconds { get; init; }
}

public sealed class StopInfo
{
    [JsonPropertyName("start_time")]
    public string StartTime { get; init; } = "";

    [JsonPropertyName("end_time")]
    public string EndTime { get; init; } = "";

    [JsonPropertyName("duration")]
    public DurationValue Duration { get; init; } = new();

    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lon { get; init; }
}
