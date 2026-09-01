using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GpxAiAnalyzer.Core.Models;

namespace GpxAiAnalyzer.Tests.Models;

public class TrackReportTests
{
    // Must mirror TrackAnalyzer.JsonOptions exactly.
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    // --- #89: explicit nulls must not overwrite the defaults ---
    [Fact]
    public void Deserialize_ExplicitNullCollections_LeavesUsableDefaults()
    {
        const string json = """
            {
              "difficulty": {"grade":"Easy","score":1,"justification":"Flat."},
              "key_segments": null,
              "recommendations": null,
              "summary": "Short flat walk.",
              "effort": null
            }
            """;

        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;

        // ReportFormatter.FormatText dereferences all of these unguarded.
        Assert.NotNull(report.KeySegments);
        Assert.Empty(report.KeySegments);
        Assert.NotNull(report.Recommendations);
        Assert.Empty(report.Recommendations);
        Assert.NotNull(report.Effort);
        Assert.NotNull(report.Difficulty);
    }

    [Fact]
    public void Deserialize_ExplicitNullDifficulty_LeavesUsableDefault()
    {
        const string json = """{"difficulty": null, "summary": "x"}""";
        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
        Assert.NotNull(report.Difficulty);
        Assert.NotNull(report.Difficulty.Grade);
    }

    [Fact]
    public void Deserialize_ExplicitNullStrings_LeaveUsableDefaults()
    {
        const string json = """
            {
              "difficulty": {"grade":null,"score":1,"justification":null},
              "key_segments": [{"type":null,"description":null}],
              "summary": null,
              "effort": {"fitness_level":null,"estimated_duration":null}
            }
            """;

        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;

        Assert.NotNull(report.Summary);
        Assert.NotNull(report.Difficulty.Grade);
        Assert.NotNull(report.Difficulty.Justification);
        Assert.NotNull(report.KeySegments[0].Type);
        Assert.NotNull(report.KeySegments[0].Description);
        Assert.NotNull(report.Effort.FitnessLevel);
        Assert.NotNull(report.Effort.EstimatedDuration);
    }

    // --- #90: French decimals must not be multiplied by 10 ---
    [Theory]
    [InlineData("\"3,2\"", 3.2)]      // French decimal
    [InlineData("\"12,75\"", 12.75)]  // French decimal
    [InlineData("\"1,200\"", 1200)]   // English thousands group
    [InlineData("\"1,200.5\"", 1200.5)]
    [InlineData("\"3.2\"", 3.2)]
    [InlineData("\"3.2 km\"", 3.2)]
    public void LenientDouble_NumericStrings_ParseToTheIntendedValue(string jsonValue, double expected)
    {
        var json = $$"""{"key_segments":[{"type":"climb","description":"d","distance_km":{{jsonValue}}}]}""";
        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
        Assert.Equal(expected, report.KeySegments[0].DistanceKm!.Value, 6);
    }

    // The machine culture here is fr-FR; pin the culture explicitly so the
    // assertion cannot pass by accident under only one of them.
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    [InlineData("")] // InvariantCulture
    public void LenientDouble_FrenchDecimalString_IsCultureIndependent(string cultureName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            const string json =
                """{"key_segments":[{"type":"climb","description":"d","distance_km":"3,2"}]}""";
            var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
            Assert.Equal(3.2, report.KeySegments[0].DistanceKm!.Value, 6);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    // --- #109: a fractional number must not throw ---
    [Fact]
    public void LenientInt_FractionalNumber_DoesNotThrow()
    {
        const string json = """
            {
              "summary": "x",
              "effort": {"fitness_level":"intermediate","estimated_duration":"6h","calorie_estimate": 1200.0}
            }
            """;

        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
        Assert.Equal(1200, report.Effort.CalorieEstimate);
    }

    [Fact]
    public void LenientInt_FractionalString_DoesNotThrow()
    {
        const string json = """
            {
              "summary": "x",
              "effort": {"fitness_level":"intermediate","estimated_duration":"6h","calorie_estimate": "1200.0"}
            }
            """;

        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
        Assert.Equal(1200, report.Effort.CalorieEstimate);
    }

    [Fact]
    public void DifficultyScore_FractionalNumber_DoesNotThrow()
    {
        const string json =
            """{"difficulty":{"grade":"Moderate","score":3.0,"justification":"j"},"summary":"x"}""";
        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts)!;
        Assert.Equal(3, report.Difficulty.Score);
    }
}
