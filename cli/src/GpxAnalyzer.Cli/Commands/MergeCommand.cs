using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Input;
using GpxAnalyzer.Cli.Core.Merge;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class MergeCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var filesArg = new Argument<string[]>("files", "GPX files, directories, or glob patterns")
        {
            Arity = ArgumentArity.OneOrMore
        };
        var outputOpt = new Option<string>("--output", () => "merged.gpx", "Output file path");
        outputOpt.AddAlias("-o");
        var sortOpt = new Option<bool>("--sort", () => true, "Sort track points by time");
        var analyzeOpt = new Option<bool>("--analyze", () => false, "Print statistics for merged result");

        // Shared compute flags (for --analyze)
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

        var cmd = new Command("merge", "Merge multiple GPX files into one")
        {
            filesArg, outputOpt, sortOpt, analyzeOpt,
            presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, maxHrOpt, maxSpeedOpt
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var files = ctx.ParseResult.GetValueForArgument(filesArg);
            var output = ctx.ParseResult.GetValueForOption(outputOpt) ?? "merged.gpx";
            var sort = ctx.ParseResult.GetValueForOption(sortOpt);
            var analyze = ctx.ParseResult.GetValueForOption(analyzeOpt);
            var format = ctx.ParseResult.GetValueForOption(formatOption) ?? "text";

            var resolvedFiles = FileResolver.ResolveFiles(files);
            if (resolvedFiles.Count == 0)
            {
                Console.Error.WriteLine("Error: no GPX files found");
                return;
            }

            var docs = new List<GpxDocument>();
            foreach (var path in resolvedFiles)
            {
                try
                {
                    docs.Add(GpxParser.ParseFile(path));
                    Console.Error.WriteLine($"  Loaded: {path}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Warning: failed to parse {path}: {ex.Message}");
                }
            }

            if (docs.Count == 0)
            {
                Console.Error.WriteLine("Error: no valid GPX files to merge");
                return;
            }

            var merged = GpxMerger.Merge(docs, sort);

            try
            {
                string? dir = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                GpxWriter.Write(output, merged.AllPoints(), "Merged");
                Console.Error.WriteLine($"Merged {docs.Count} files -> {output} ({merged.AllPoints().Count} points)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error writing {output}: {ex.Message}");
                return;
            }

            if (analyze)
            {
                var formatter = FormatterFactory.Create(format);
                var cfg = SharedFlags.BuildConfigFromContext(ctx, presetOpt, stopSpeedOpt, stopDurationOpt,
                    elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                    demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                    maxHrOpt, maxSpeedOpt);

                var (summary, _) = ComputePipeline.Compute(merged.AllPoints(), merged.SegmentCount(), cfg);
                formatter.Format(Console.Out, output, summary, cfg.StopConfig);
            }
        });

        return cmd;
    }
}
