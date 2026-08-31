namespace GpxAiAnalyzer.Core.Models;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Structured AI output: the report produced by the analysis agent.
/// </summary>
public sealed class TrackReport
{
    private readonly DifficultyRating _difficulty = new();
    private readonly List<KeySegment> _keySegments = [];
    private readonly List<string> _recommendations = [];
    private readonly string _summary = "";
    private readonly EffortEstimate _effort = new();

    // System.Text.Json assigns an explicit JSON null over the initializer, so the
    // default has to be re-applied in the setter. Every consumer (ReportFormatter)
    // dereferences these unguarded.
    [JsonPropertyName("difficulty")]
    public DifficultyRating Difficulty
    {
        get => _difficulty;
        init => _difficulty = value ?? new();
    }

    [JsonPropertyName("key_segments")]
    public List<KeySegment> KeySegments
    {
        get => _keySegments;
        init => _keySegments = value ?? [];
    }

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations
    {
        get => _recommendations;
        init => _recommendations = value ?? [];
    }

    [JsonPropertyName("summary")]
    public string Summary
    {
        get => _summary;
        init => _summary = value ?? "";
    }

    [JsonPropertyName("effort")]
    public EffortEstimate Effort
    {
        get => _effort;
        init => _effort = value ?? new();
    }
}

public sealed class DifficultyRating
{
    private readonly string _grade = "";
    private readonly string _justification = "";

    [JsonPropertyName("grade")]
    public string Grade
    {
        get => _grade;
        init => _grade = value ?? "";
    }

    [JsonPropertyName("score")]
    [JsonConverter(typeof(LenientIntNonNullConverter))]
    public int Score { get; init; }

    [JsonPropertyName("justification")]
    public string Justification
    {
        get => _justification;
        init => _justification = value ?? "";
    }
}

public sealed class KeySegment
{
    private readonly string _type = "";
    private readonly string _description = "";

    [JsonPropertyName("type")]
    public string Type
    {
        get => _type;
        init => _type = value ?? "";
    }

    [JsonPropertyName("description")]
    public string Description
    {
        get => _description;
        init => _description = value ?? "";
    }

    [JsonPropertyName("elevation_change")]
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? ElevationChange { get; init; }

    [JsonPropertyName("distance_km")]
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double? DistanceKm { get; init; }
}

public sealed class EffortEstimate
{
    private readonly string _fitnessLevel = "";
    private readonly string _estimatedDuration = "";

    [JsonPropertyName("fitness_level")]
    public string FitnessLevel
    {
        get => _fitnessLevel;
        init => _fitnessLevel = value ?? "";
    }

    [JsonPropertyName("estimated_duration")]
    public string EstimatedDuration
    {
        get => _estimatedDuration;
        init => _estimatedDuration = value ?? "";
    }

    [JsonPropertyName("calorie_estimate")]
    [JsonConverter(typeof(LenientIntConverter))]
    public int? CalorieEstimate { get; init; }
}

/// <summary>
/// Normalises the numeric strings an LLM writes into an invariant-parseable form.
/// </summary>
internal static class LenientNumber
{
    /// <summary>
    /// Normalises an LLM-written numeric string. A comma is a thousands separator
    /// in English output and a DECIMAL separator in French output, and this project
    /// asks the model to answer in French — so stripping commas unconditionally
    /// turned "3,2" into "32". Disambiguate by shape instead.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();

        // Strip anything that is not a digit, sign, dot or comma (units like
        // " km", " kcal", thin spaces used as French group separators).
        s = new string(s.Where(c => char.IsDigit(c) || c is '-' or '+' or '.' or ',').ToArray());
        if (s.Length == 0) return null;

        bool hasDot = s.Contains('.');
        bool hasComma = s.Contains(',');

        if (hasDot && hasComma)
        {
            // Both present: the LAST one is the decimal separator.
            char dec = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
            char grp = dec == '.' ? ',' : '.';
            s = s.Replace(grp.ToString(), string.Empty).Replace(dec, '.');
        }
        else if (hasComma)
        {
            // A single comma with 1-2 trailing digits is a French decimal ("3,2",
            // "12,75"). Exactly three trailing digits is an English group ("1,200").
            int idx = s.LastIndexOf(',');
            int trailing = s.Length - idx - 1;
            s = s.Count(c => c == ',') == 1 && trailing is 1 or 2
                ? s.Replace(',', '.')
                : s.Replace(",", string.Empty);
        }

        return s;
    }
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
            var s = LenientNumber.Normalize(reader.GetString());
            if (s is not null &&
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
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
        {
            // GetInt32() throws FormatException on any non-Int32-representable
            // number — a decimal point is enough — which defeats the converter's
            // own leniency contract. "calorie_estimate": 1200.0 is common.
            if (reader.TryGetInt32(out var i)) return i;
            if (reader.TryGetDouble(out var d) && d >= int.MinValue && d <= int.MaxValue)
                return (int)Math.Round(d);
            return null;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = LenientNumber.Normalize(reader.GetString());
            if (s is null) return null;
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            // "1200.0" as a string is the same case as the Number branch above.
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv)
                && dv >= int.MinValue && dv <= int.MaxValue)
                return (int)Math.Round(dv);
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

/// <summary>Non-nullable companion to LenientIntConverter; unparseable values become 0.</summary>
public sealed class LenientIntNonNullConverter : JsonConverter<int>
{
    private static readonly LenientIntConverter Inner = new();

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Inner.Read(ref reader, typeof(int?), options) ?? 0;

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
