using System.CommandLine;
using System.Globalization;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Split;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class SplitCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var fileArg = new Argument<string>("file") { Description = "GPX file to split" };
        var intervalOpt = new Option<string>("--interval") { Description = "Split interval (e.g. 24h, 12h, 30m)", DefaultValueFactory = _ => "24h" };
        var outputDirOpt = new Option<string>("--output-dir") { Description = "Output directory for split files", DefaultValueFactory = _ => "splits" };
        var prefixOpt = new Option<string>("--prefix") { Description = "Filename prefix for split files", DefaultValueFactory = _ => "segment" };

        // Shared compute flags
        var presetOpt = new Option<string>("--preset") { Description = "Stop detection preset: hiking, trail, cycling", DefaultValueFactory = _ => "hiking" };
        var stopSpeedOpt = new Option<double>("--stop-speed") { Description = "Override max speed for stops (m/s)", DefaultValueFactory = _ => 0 };
        var stopDurationOpt = new Option<double>("--stop-duration") { Description = "Override min duration for stops (seconds)", DefaultValueFactory = _ => 0 };
        var elevThresholdOpt = new Option<double>("--elevation-threshold") { Description = "Min elevation change (meters)", DefaultValueFactory = _ => 2.0 };
        var smoothingOpt = new Option<string>("--smoothing") { Description = "Elevation smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "medium" };
        var demDirOpt = new Option<string>("--dem-dir") { Description = "SRTM .hgt directory", DefaultValueFactory = _ => "" };
        var demCacheOpt = new Option<string>("--dem-cache") { Description = "DEM cache directory", DefaultValueFactory = _ => "" };
        var demAutoOpt = new Option<bool>("--dem-auto-download") { Description = "Auto-download missing tiles", DefaultValueFactory = _ => true };
        var demMaxMemOpt = new Option<int>("--dem-max-memory") { Description = "Max memory for DEM (MB, 0=unlimited)", DefaultValueFactory = _ => 0 };
        var demSkipValOpt = new Option<bool>("--dem-skip-validation") { Description = "Skip tile validation", DefaultValueFactory = _ => false };
        var elevAlgoOpt = new Option<string>("--elevation-algo") { Description = "Algorithm: threshold, douglas-peucker, segments", DefaultValueFactory = _ => "threshold" };
        var trackSmoothOpt = new Option<string>("--track-smoothing") { Description = "GPS lat/lon smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "none" };
        var dpEpsOpt = new Option<double>("--dp-epsilon") { Description = "Douglas-Peucker epsilon (meters)", DefaultValueFactory = _ => 3.0 };
        var segMinLenOpt = new Option<double>("--seg-min-length") { Description = "Segments min length (meters)", DefaultValueFactory = _ => 200.0 };
        var segMaxDevOpt = new Option<double>("--seg-max-deviation") { Description = "Segments max RMS residual (meters)", DefaultValueFactory = _ => 2.0 };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var maxSpeedOpt = new Option<double>("--max-speed") { Description = "GPS outlier removal threshold (m/s)", DefaultValueFactory = _ => 0 };

        var cmd = new Command("split", "Split a GPX file by time interval")
        {
            fileArg, intervalOpt, outputDirOpt, prefixOpt,
            presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, maxHrOpt, maxSpeedOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var file = parseResult.GetRequiredValue(fileArg);
            var interval = parseResult.GetValue(intervalOpt) ?? "24h";
            var outputDir = parseResult.GetValue(outputDirOpt) ?? "splits";
            var prefix = parseResult.GetValue(prefixOpt) ?? "segment";
            var format = parseResult.GetValue(formatOption) ?? "text";

            var splitInterval = ParseDuration(interval);
            if (splitInterval <= TimeSpan.Zero)
            {
                Console.Error.WriteLine(
                    $"Error: invalid interval '{interval}' - use a unit suffix, e.g. 24h, 90m or 30s");
                return 1;
            }

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var cfg = SharedFlags.BuildConfigFromParseResult(parseResult, presetOpt, stopSpeedOpt, stopDurationOpt,
                elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                maxHrOpt, maxSpeedOpt);

            try
            {
                var doc = GpxParser.ParseFile(file);
                var points = doc.AllPoints();
                var segments = TimeSplitter.ByTime(points, splitInterval);

                Console.Error.WriteLine($"Split into {segments.Count} segments (interval: {interval})");
                Directory.CreateDirectory(outputDir);

                for (int i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    string filename = $"{prefix}-{i + 1:D3}.gpx";
                    string outPath = Path.Combine(outputDir, filename);

                    try
                    {
                        GpxWriter.Write(outPath, seg.Points, $"{prefix}-{i + 1:D3}");
                        Console.Error.WriteLine($"  {filename} ({seg.Points.Count} points)");

                        // Compute on a copy: ComputePipeline mutates Ele and Lat/Lon
                        // in place (DEM correction, elevation and track smoothing),
                        // and boundary points are shared between adjacent segments.
                        var forAnalysis = seg.Points.Select(p => p.Clone()).ToList();
                        var (summary, _) = ComputePipeline.Compute(forAnalysis, 1, cfg);
                        formatter.Format(Console.Out, filename, summary, cfg.StopConfig);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  Error processing segment {i + 1}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }

            return 0;
        });

        return cmd;
    }

    internal static TimeSpan ParseDuration(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith("h") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
            return TimeSpan.FromHours(hours);
        if (s.EndsWith("m") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
            return TimeSpan.FromMinutes(minutes);
        if (s.EndsWith("s") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        // Deliberately NOT falling through to TimeSpan.TryParse: its format reads a
        // bare integer as a whole number of DAYS, so '--interval 24' was silently
        // accepted as 24 days instead of the 24 hours the user meant.
        return TimeSpan.Zero;
    }
}
