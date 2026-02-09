namespace GpxAiAnalyzer.Core.Models;

using System.Globalization;
using System.Text.Json;
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
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? ElevationChange { get; init; }

    [JsonPropertyName("distance_km")]
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? DistanceKm { get; init; }
}

public sealed class EffortEstimate
{
    [JsonPropertyName("fitness_level")]
    public string FitnessLevel { get; init; } = "";

    [JsonPropertyName("estimated_duration")]
    public string EstimatedDuration { get; init; } = "";

    [JsonPropertyName("calorie_estimate")]
    [JsonConverter(typeof(LenientIntConverter))]
    public int? CalorieEstimate { get; init; }
}

/// <summary>
/// Lenient converter for nullable double: handles numbers, numeric strings, and non-numeric strings (returns null).
/// </summary>
public sealed class LenientDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString()?.Trim().Replace(",", "");
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            return null;
        }
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

/// <summary>
/// Lenient converter for nullable int: handles numbers, numeric strings, and non-numeric strings (returns null).
/// </summary>
public sealed class LenientIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt32();
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString()?.Trim().Replace(",", "");
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            return null;
        }
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}
