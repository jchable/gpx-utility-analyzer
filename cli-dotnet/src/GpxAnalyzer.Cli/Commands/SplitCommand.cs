using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Gpx;
using GpxAnalyzer.Cli.Output;
using GpxAnalyzer.Cli.Split;
using GpxAnalyzer.Cli.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class SplitCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var fileArg = new Argument<string>("file", "GPX file to split");
        var intervalOpt = new Option<string>("--interval", () => "24h", "Split interval (e.g. 24h, 12h, 30m)");
        var outputDirOpt = new Option<string>("--output-dir", () => "splits", "Output directory for split files");
        var prefixOpt = new Option<string>("--prefix", () => "segment", "Filename prefix for split files");

        // Shared compute flags
        var presetOpt = new Option<string>("--preset", () => "hiking", "Stop detection preset: hiking, trail, cycling");
        var stopSpeedOpt = new Option<double>("--stop-speed", () => 0, "Override max speed for stops (m/s)");
        var stopDurationOpt = new Option<double>("--stop-duration", () => 0, "Override min duration for stops (seconds)");
        var elevThresholdOpt = new Option<double>("--elevation-threshold", () => 2.0, "Min elevation change (meters)");
        var smoothingOpt = new Option<string>("--smoothing", () => "medium", "Elevation smoothing: none, light, medium, heavy");
        var demDirOpt = new Option<string>("--dem-dir", () => "", "SRTM .hgt directory");
        var demCacheOpt = new Option<string>("--dem-cache", () => "", "DEM cache directory");
        var demAutoOpt = new Option<bool>("--dem-auto-download", () => true, "Auto-download missing tiles");
        var demMaxMemOpt = new Option<int>("--dem-max-memory", () => 0, "Max memory for DEM (MB, 0=unlimited)");
        var demSkipValOpt = new Option<bool>("--dem-skip-validation", () => false, "Skip tile validation");
        var elevAlgoOpt = new Option<string>("--elevation-algo", () => "threshold", "Algorithm: threshold, douglas-peucker, segments");
        var trackSmoothOpt = new Option<string>("--track-smoothing", () => "none", "GPS lat/lon smoothing: none, light, medium, heavy");
        var dpEpsOpt = new Option<double>("--dp-epsilon", () => 3.0, "Douglas-Peucker epsilon (meters)");
        var segMinLenOpt = new Option<double>("--seg-min-length", () => 200.0, "Segments min length (meters)");
        var segMaxDevOpt = new Option<double>("--seg-max-deviation", () => 2.0, "Segments max RMS residual (meters)");
        var maxHrOpt = new Option<int>("--max-hr", () => 0, "Max HR for zone calculation");
        var maxSpeedOpt = new Option<double>("--max-speed", () => 0, "GPS outlier removal threshold (m/s)");

        var cmd = new Command("split", "Split a GPX file by time interval")
        {
            fileArg, intervalOpt, outputDirOpt, prefixOpt,
            presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, maxHrOpt, maxSpeedOpt
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var file = ctx.ParseResult.GetValueForArgument(fileArg);
            var interval = ctx.ParseResult.GetValueForOption(intervalOpt) ?? "24h";
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOpt) ?? "splits";
            var prefix = ctx.ParseResult.GetValueForOption(prefixOpt) ?? "segment";
            var format = ctx.ParseResult.GetValueForOption(formatOption) ?? "text";

            var splitInterval = ParseDuration(interval);
            if (splitInterval <= TimeSpan.Zero)
            {
                Console.Error.WriteLine($"Error: invalid interval '{interval}'");
                return;
            }

            var formatter = FormatterFactory.Create(format);
            var cfg = SharedFlags.BuildConfigFromContext(ctx, presetOpt, stopSpeedOpt, stopDurationOpt,
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

                        var (summary, _) = ComputePipeline.Compute(seg.Points, 1, cfg);
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
        });

        return cmd;
    }

    private static TimeSpan ParseDuration(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith("h") && double.TryParse(s[..^1], out var hours))
            return TimeSpan.FromHours(hours);
        if (s.EndsWith("m") && double.TryParse(s[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);
        if (s.EndsWith("s") && double.TryParse(s[..^1], out var seconds))
            return TimeSpan.FromSeconds(seconds);
        if (TimeSpan.TryParse(s, out var ts))
            return ts;
        return TimeSpan.Zero;
    }
}
