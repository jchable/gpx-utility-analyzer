namespace GpxAiAnalyzer.Analysis;

using GpxAiAnalyzer.Models;
using System.Globalization;
using System.Text;

/// <summary>
/// Constructs the analysis prompt from GPX statistics.
/// Uses invariant culture for consistent decimal formatting across locales.
/// </summary>
public static class PromptBuilder
{
    private const int MaxStopsInPrompt = 10;

    public static string BuildAnalysisPrompt(GpxStats stats)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following GPS track statistics and produce a structured assessment.");
        sb.AppendLine();
        sb.AppendLine("## Track Data");
        sb.AppendLine(ci, $"- File: {stats.Filename}");
        sb.AppendLine(ci, $"- Distance: {stats.TotalDistanceKm:F1} km (2D), {stats.TotalDistance3dM / 1000:F1} km (3D)");
        sb.AppendLine(ci, $"- Elevation: +{stats.ElevationGainM:F0}m / -{stats.ElevationLossM:F0}m");
        sb.AppendLine(ci, $"- Elevation range: {stats.MinElevationM:F0}m to {stats.MaxElevationM:F0}m");
        sb.AppendLine(ci, $"- Time: {stats.TotalTime.Display} total, {stats.MovingTime.Display} moving, {stats.StoppedTime.Display} stopped");
        sb.AppendLine(ci, $"- Speed: {stats.AvgSpeedKmh:F1} km/h avg, {stats.AvgMovingSpeedKmh:F1} km/h moving, {stats.MaxSpeedKmh:F1} km/h max");
        sb.AppendLine(ci, $"- Pace: {stats.AvgPace} avg, {stats.AvgMovingPace} moving");
        sb.AppendLine(ci, $"- Stops: {stats.StopCount} stops, {stats.TotalStopTime.Display} total stop time");
        sb.AppendLine(ci, $"- Points: {stats.PointCount} points, {stats.SegmentCount} segments, {stats.PointsPerKm:F1} pts/km");

        if (stats.LongestStop is not null)
        {
            sb.AppendLine(ci, $"- Longest stop: {stats.LongestStop.Duration.Display} at ({stats.LongestStop.Lat:F5}, {stats.LongestStop.Lon:F5})");
        }

        if (stats.Stops is { Count: > 0 })
        {
            var stopsToShow = stats.Stops.Take(MaxStopsInPrompt);
            var stopsList = string.Join("; ", stopsToShow.Select(s =>
                string.Format(ci, "({0:F4},{1:F4}) for {2}", s.Lat, s.Lon, s.Duration.Display)));
            sb.AppendLine($"- Stop locations: {stopsList}");
            if (stats.Stops.Count > MaxStopsInPrompt)
            {
                sb.AppendLine($"  ... and {stats.Stops.Count - MaxStopsInPrompt} more stops");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Required Analysis");
        sb.AppendLine("Use the available tools (EstimateDifficulty, ClassifyActivity, GetSteepnessRatio, GetStopFrequency) to compute derived metrics, then provide a structured JSON report with:");
        sb.AppendLine("1. **difficulty**: grade (T1-T6 SAC scale or Easy/Moderate/Strenuous/Expert), score (1-10), justification");
        sb.AppendLine("2. **key_segments**: notable climb/descent/flat/technical sections with elevation_change and distance_km");
        sb.AppendLine("3. **recommendations**: practical advice for someone attempting this track");
        sb.AppendLine("4. **summary**: 2-3 sentence overview of the track character");
        sb.AppendLine("5. **effort**: fitness_level (beginner/intermediate/advanced), estimated_duration, calorie_estimate");

        return sb.ToString();
    }
}
