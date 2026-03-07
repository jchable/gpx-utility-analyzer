namespace GpxAiAnalyzer.Core.Analysis;

using GpxAiAnalyzer.Core.Models;
using System.Globalization;
using System.Text;

/// <summary>
/// Constructs the analysis prompt from GPX statistics.
/// Uses invariant culture for consistent decimal formatting across locales.
/// </summary>
public static class PromptBuilder
{
    private const int MaxStopsInPrompt = 10;

    public static string BuildAnalysisPrompt(GpxStats stats, string language = "en")
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

        // Biometrics
        if (stats.HeartRate is not null)
        {
            sb.AppendLine(ci, $"- Heart Rate: {stats.HeartRate.AvgBpm:F0} bpm avg, {stats.HeartRate.MaxBpm} bpm max, {stats.HeartRate.MinBpm} bpm min");
            if (stats.HeartRate.Zones is { Count: > 0 })
            {
                var zoneStr = string.Join(", ", stats.HeartRate.Zones.Select(z =>
                    string.Format(ci, "{0}: {1}", z.Name, z.Duration.Display)));
                sb.AppendLine($"- HR Zones: {zoneStr}");
            }
        }
        if (stats.Power is not null)
        {
            sb.AppendLine(ci, $"- Power: {stats.Power.AvgWatts:F0}W avg, {stats.Power.MaxWatts}W max, {stats.Power.NormalizedPowerWatts:F0}W NP");
        }
        if (stats.Cadence is not null)
        {
            sb.AppendLine(ci, $"- Cadence: {stats.Cadence.AvgRpm:F0} rpm avg, {stats.Cadence.MaxRpm} rpm max");
        }
        if (stats.Temperature is not null)
        {
            sb.AppendLine(ci, $"- Temperature: {stats.Temperature.AvgCelsius:F1}C avg, {stats.Temperature.MinCelsius:F1}C min, {stats.Temperature.MaxCelsius:F1}C max");
        }

        // Data quality / anomalies
        if (stats.Anomalies is { TotalCount: > 0 } anomalies)
        {
            sb.AppendLine();
            sb.AppendLine("## Data Quality");
            sb.AppendLine(ci, $"- Quality Score: {anomalies.QualityScore}/100");
            sb.AppendLine(ci, $"- Anomalies: {anomalies.CriticalCount} critical, {anomalies.WarningCount} warnings, {anomalies.InfoCount} info");
            if (Math.Abs(anomalies.DistanceImpactM) >= 1)
                sb.AppendLine(ci, $"- Estimated distance impact: {anomalies.DistanceImpactM:F0} m");
            if (Math.Abs(anomalies.TimeImpactS) >= 1)
                sb.AppendLine(ci, $"- Estimated time impact: {anomalies.TimeImpactS:F0} s");
            if (anomalies.CorrectionApplied)
                sb.AppendLine("- Corrections have been applied");

            // Show top 5 warning/critical anomalies
            if (anomalies.Anomalies is { Count: > 0 })
            {
                var significant = anomalies.Anomalies
                    .Where(a => a.Severity is "critical" or "warning")
                    .Take(5)
                    .ToList();
                foreach (var a in significant)
                {
                    string timeRange = !string.IsNullOrEmpty(a.StartTime) ? $"{a.StartTime} - {a.EndTime}" : $"points {a.StartIndex}-{a.EndIndex}";
                    sb.AppendLine(ci, $"  - [{a.Severity.ToUpperInvariant()}] {a.Type} ({timeRange}): {a.Description}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Required Analysis");
        sb.AppendLine("Use the available tools (EstimateDifficulty, ClassifyActivity, GetSteepnessRatio, GetStopFrequency, and biometric tools if data is available) to compute derived metrics, then provide a structured JSON report with:");
        sb.AppendLine("1. **difficulty**: grade (T1-T6 SAC scale or Easy/Moderate/Strenuous/Expert), score (1-10), justification");
        sb.AppendLine("2. **key_segments**: notable climb/descent/flat/technical sections with elevation_change and distance_km");
        sb.AppendLine("3. **recommendations**: practical advice for someone attempting this track");
        sb.AppendLine("4. **summary**: 2-3 sentence overview of the track character");
        sb.AppendLine("5. **effort**: fitness_level (beginner/intermediate/advanced), estimated_duration, calorie_estimate");

        if (!string.IsNullOrEmpty(language) && !language.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            var langName = language.ToLowerInvariant() switch
            {
                "fr" => "French (français)",
                _ => language,
            };
            sb.AppendLine();
            sb.AppendLine($"IMPORTANT: Respond entirely in {langName}. All text content in your response (summary, descriptions, recommendations, segment names) must be written in {langName}.");
        }

        return sb.ToString();
    }
}
