namespace GpxAiAnalyzer.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Deserialization model matching the CLI JSON output exactly.
/// Contract defined in cli/src/GpxAnalyzer.Cli.Core/Output/JsonModels.cs (JsonSummary class).
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

    // Biometrics (optional — null when GPX has no extension data)
    [JsonPropertyName("heart_rate")]
    public HeartRateStats? HeartRate { get; init; }

    [JsonPropertyName("power")]
    public PowerStats? Power { get; init; }

    [JsonPropertyName("cadence")]
    public CadenceStats? Cadence { get; init; }

    [JsonPropertyName("temperature")]
    public TemperatureStats? Temperature { get; init; }

    // Effort metrics
    [JsonPropertyName("effort")]
    public EffortStatsModel? Effort { get; init; }
}

public sealed class HeartRateStats
{
    [JsonPropertyName("avg_bpm")]
    public double AvgBpm { get; init; }

    [JsonPropertyName("max_bpm")]
    public int MaxBpm { get; init; }

    [JsonPropertyName("min_bpm")]
    public int MinBpm { get; init; }

    [JsonPropertyName("zones")]
    public List<HeartRateZoneInfo>? Zones { get; init; }
}

public sealed class HeartRateZoneInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("min_percent")]
    public int MinPercent { get; init; }

    [JsonPropertyName("max_percent")]
    public int MaxPercent { get; init; }

    [JsonPropertyName("duration")]
    public DurationValue Duration { get; init; } = new();
}

public sealed class PowerStats
{
    [JsonPropertyName("avg_watts")]
    public double AvgWatts { get; init; }

    [JsonPropertyName("max_watts")]
    public int MaxWatts { get; init; }

    [JsonPropertyName("normalized_power_watts")]
    public double NormalizedPowerWatts { get; init; }
}

public sealed class CadenceStats
{
    [JsonPropertyName("avg_rpm")]
    public double AvgRpm { get; init; }

    [JsonPropertyName("max_rpm")]
    public int MaxRpm { get; init; }
}

public sealed class TemperatureStats
{
    [JsonPropertyName("avg_celsius")]
    public double AvgCelsius { get; init; }

    [JsonPropertyName("min_celsius")]
    public double MinCelsius { get; init; }

    [JsonPropertyName("max_celsius")]
    public double MaxCelsius { get; init; }
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

public sealed class EffortStatsModel
{
    [JsonPropertyName("naismith_time")]
    public DurationValue NaismithTime { get; init; } = new();

    [JsonPropertyName("tobler_time")]
    public DurationValue ToblerTime { get; init; } = new();

    [JsonPropertyName("munter_time")]
    public DurationValue MunterTime { get; init; } = new();

    [JsonPropertyName("performance_ratio_naismith")]
    public double PerformanceRatioNaismith { get; init; }

    [JsonPropertyName("performance_ratio_tobler")]
    public double PerformanceRatioTobler { get; init; }

    [JsonPropertyName("kilometre_effort")]
    public double KilometreEffort { get; init; }

    [JsonPropertyName("itra_points")]
    public double ItraPoints { get; init; }

    [JsonPropertyName("itra_category")]
    public string ItraCategory { get; init; } = "";

    [JsonPropertyName("equivalent_flat_distance_km")]
    public double EquivalentFlatDistanceKm { get; init; }

    [JsonPropertyName("terrain_difficulty")]
    public TerrainDifficultyModel TerrainDifficulty { get; init; } = new();
}

public sealed class TerrainDifficultyModel
{
    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("grade")]
    public string Grade { get; init; } = "";

    [JsonPropertyName("avg_grade_percent")]
    public double AvgGradePercent { get; init; }

    [JsonPropertyName("max_grade_percent")]
    public double MaxGradePercent { get; init; }

    [JsonPropertyName("grade_variance")]
    public double GradeVariance { get; init; }

    [JsonPropertyName("steep_section_ratio")]
    public double SteepSectionRatio { get; init; }

    [JsonPropertyName("elevation_per_km")]
    public double ElevationPerKm { get; init; }
}
