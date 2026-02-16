using System.Text.Json.Serialization;

namespace GpxAnalyzer.Cli.Core.Output;

public sealed class JsonSummary
{
    [JsonPropertyName("filename")]
    public string Filename { get; init; } = "";

    [JsonPropertyName("total_distance_m")]
    public double TotalDistanceM { get; init; }

    [JsonPropertyName("total_distance_3d_m")]
    public double TotalDistance3dM { get; init; }

    [JsonPropertyName("total_distance_km")]
    public double TotalDistanceKm { get; init; }

    [JsonPropertyName("elevation_gain_m")]
    public double ElevationGainM { get; init; }

    [JsonPropertyName("elevation_loss_m")]
    public double ElevationLossM { get; init; }

    [JsonPropertyName("max_elevation_m")]
    public double MaxElevationM { get; init; }

    [JsonPropertyName("min_elevation_m")]
    public double MinElevationM { get; init; }

    [JsonPropertyName("start_time")]
    public string StartTime { get; init; } = "";

    [JsonPropertyName("end_time")]
    public string EndTime { get; init; } = "";

    [JsonPropertyName("total_time")]
    public JsonDuration TotalTime { get; init; } = new();

    [JsonPropertyName("moving_time")]
    public JsonDuration MovingTime { get; init; } = new();

    [JsonPropertyName("stopped_time")]
    public JsonDuration StoppedTime { get; init; } = new();

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

    [JsonPropertyName("point_count")]
    public int PointCount { get; init; }

    [JsonPropertyName("filtered_points")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FilteredPoints { get; init; }

    [JsonPropertyName("segment_count")]
    public int SegmentCount { get; init; }

    [JsonPropertyName("points_per_km")]
    public double PointsPerKm { get; init; }

    [JsonPropertyName("stop_count")]
    public int StopCount { get; init; }

    [JsonPropertyName("total_stop_time")]
    public JsonDuration TotalStopTime { get; init; } = new();

    [JsonPropertyName("avg_stop_duration")]
    public JsonDuration AvgStopDuration { get; init; } = new();

    [JsonPropertyName("longest_stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonStop? LongestStop { get; init; }

    [JsonPropertyName("stops")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonStop>? Stops { get; init; }

    [JsonPropertyName("heart_rate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonHeartRate? HeartRate { get; init; }

    [JsonPropertyName("power")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonPower? Power { get; init; }

    [JsonPropertyName("cadence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonCadence? Cadence { get; init; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonTemperature? Temperature { get; init; }
}

public sealed class JsonDuration
{
    [JsonPropertyName("display")]
    public string Display { get; init; } = "";

    [JsonPropertyName("seconds")]
    public double Seconds { get; init; }
}

public sealed class JsonStop
{
    [JsonPropertyName("start_time")]
    public string StartTime { get; init; } = "";

    [JsonPropertyName("end_time")]
    public string EndTime { get; init; } = "";

    [JsonPropertyName("duration")]
    public JsonDuration Duration { get; init; } = new();

    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lon { get; init; }
}

public sealed class JsonHeartRate
{
    [JsonPropertyName("avg_bpm")]
    public double AvgBpm { get; init; }

    [JsonPropertyName("max_bpm")]
    public int MaxBpm { get; init; }

    [JsonPropertyName("min_bpm")]
    public int MinBpm { get; init; }

    [JsonPropertyName("zones")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonHRZone>? Zones { get; init; }
}

public sealed class JsonHRZone
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("min_percent")]
    public int MinPercent { get; init; }

    [JsonPropertyName("max_percent")]
    public int MaxPercent { get; init; }

    [JsonPropertyName("duration")]
    public JsonDuration Duration { get; init; } = new();
}

public sealed class JsonPower
{
    [JsonPropertyName("avg_watts")]
    public double AvgWatts { get; init; }

    [JsonPropertyName("max_watts")]
    public int MaxWatts { get; init; }

    [JsonPropertyName("normalized_power_watts")]
    public double NormalizedPowerWatts { get; init; }
}

public sealed class JsonCadence
{
    [JsonPropertyName("avg_rpm")]
    public double AvgRpm { get; init; }

    [JsonPropertyName("max_rpm")]
    public int MaxRpm { get; init; }
}

public sealed class JsonTemperature
{
    [JsonPropertyName("avg_celsius")]
    public double AvgCelsius { get; init; }

    [JsonPropertyName("min_celsius")]
    public double MinCelsius { get; init; }

    [JsonPropertyName("max_celsius")]
    public double MaxCelsius { get; init; }
}
