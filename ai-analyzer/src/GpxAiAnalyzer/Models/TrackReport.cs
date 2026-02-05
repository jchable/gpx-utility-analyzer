namespace GpxAiAnalyzer.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Structured AI output: the report produced by the analysis agent.
/// </summary>
public sealed class TrackReport
{
    [JsonPropertyName("difficulty")]
    public DifficultyRating Difficulty { get; init; } = new();

    [JsonPropertyName("key_segments")]
    public List<KeySegment> KeySegments { get; init; } = [];

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("effort")]
    public EffortEstimate Effort { get; init; } = new();
}

public sealed class DifficultyRating
{
    [JsonPropertyName("grade")]
    public string Grade { get; init; } = "";

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("justification")]
    public string Justification { get; init; } = "";
}

public sealed class KeySegment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("elevation_change")]
    public double? ElevationChange { get; init; }

    [JsonPropertyName("distance_km")]
    public double? DistanceKm { get; init; }
}

public sealed class EffortEstimate
{
    [JsonPropertyName("fitness_level")]
    public string FitnessLevel { get; init; } = "";

    [JsonPropertyName("estimated_duration")]
    public string EstimatedDuration { get; init; } = "";

    [JsonPropertyName("calorie_estimate")]
    public int? CalorieEstimate { get; init; }
}
