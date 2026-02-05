namespace GpxAiAnalyzer.Output;

using GpxAiAnalyzer.Models;
using System.Text.Json;

public static class ReportFormatter
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Format(TextWriter writer, string filename, TrackReport report, string format)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            FormatJson(writer, filename, report);
        }
        else
        {
            FormatText(writer, filename, report);
        }
    }

    private static void FormatJson(TextWriter writer, string filename, TrackReport report)
    {
        var output = new { filename, report };
        var json = JsonSerializer.Serialize(output, JsonWriteOptions);
        writer.WriteLine(json);
    }

    private static void FormatText(TextWriter writer, string filename, TrackReport report)
    {
        writer.WriteLine();
        writer.WriteLine($"=== AI Track Analysis: {filename} ===");
        writer.WriteLine();

        // Summary
        writer.WriteLine("Summary");
        writer.WriteLine($"  {report.Summary}");
        writer.WriteLine();

        // Difficulty
        writer.WriteLine("Difficulty");
        writer.WriteLine($"  Grade: {report.Difficulty.Grade}  (Score: {report.Difficulty.Score}/10)");
        writer.WriteLine($"  {report.Difficulty.Justification}");
        writer.WriteLine();

        // Key Segments
        if (report.KeySegments.Count > 0)
        {
            writer.WriteLine("Key Segments");
            foreach (var seg in report.KeySegments)
            {
                var details = new List<string>();
                if (seg.DistanceKm.HasValue) details.Add($"{seg.DistanceKm:F1} km");
                if (seg.ElevationChange.HasValue) details.Add($"{seg.ElevationChange:+0;-0}m");
                var detailStr = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
                writer.WriteLine($"  [{seg.Type}] {seg.Description}{detailStr}");
            }
            writer.WriteLine();
        }

        // Effort
        writer.WriteLine("Effort Estimate");
        writer.WriteLine($"  Fitness level: {report.Effort.FitnessLevel}");
        writer.WriteLine($"  Estimated duration: {report.Effort.EstimatedDuration}");
        if (report.Effort.CalorieEstimate.HasValue)
            writer.WriteLine($"  Calories: ~{report.Effort.CalorieEstimate} kcal");
        writer.WriteLine();

        // Recommendations
        if (report.Recommendations.Count > 0)
        {
            writer.WriteLine("Recommendations");
            foreach (var rec in report.Recommendations)
            {
                writer.WriteLine($"  - {rec}");
            }
        }
    }
}
