using System.CommandLine;
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
        var filesArg = new Argument<string[]>("files")
        {
            Description = "GPX files, directories, or glob patterns",
            Arity = ArgumentArity.OneOrMore
        };
        var outputOpt = new Option<string>("--output", "-o") { Description = "Output file path", DefaultValueFactory = _ => "merged.gpx" };
        var sortOpt = new Option<bool>("--sort") { Description = "Sort track points by time", DefaultValueFactory = _ => true };
        var analyzeOpt = new Option<bool>("--analyze") { Description = "Print statistics for merged result", DefaultValueFactory = _ => false };

        // Shared compute flags (for --analyze)
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

        var cmd = new Command("merge", "Merge multiple GPX files into one")
        {
            filesArg, outputOpt, sortOpt, analyzeOpt,
            presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, maxHrOpt, maxSpeedOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var files = parseResult.GetRequiredValue(filesArg);
            var output = parseResult.GetValue(outputOpt) ?? "merged.gpx";
            var sort = parseResult.GetValue(sortOpt);
            var analyze = parseResult.GetValue(analyzeOpt);
            var format = parseResult.GetValue(formatOption) ?? "text";

            var resolvedFiles = FileResolver.ResolveFiles(files);
            if (resolvedFiles.Count == 0)
            {
                Console.Error.WriteLine("Error: no GPX files found");
                return 1;
            }

            var docs = new List<GpxDocument>();
            int failures = 0;
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
                    failures++;
                }
            }

            if (docs.Count == 0)
            {
                Console.Error.WriteLine("Error: no valid GPX files to merge");
                return 1;
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
                return 1;
            }

            if (analyze)
            {
                var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
                var cfg = SharedFlags.BuildConfigFromParseResult(parseResult, presetOpt, stopSpeedOpt, stopDurationOpt,
                    elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                    demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                    maxHrOpt, maxSpeedOpt);

                var (summary, _) = ComputePipeline.Compute(merged.AllPoints(), merged.SegmentCount(), cfg);
                formatter.Format(Console.Out, output, summary, cfg.StopConfig);
            }

            // #139: a merge that silently dropped one of its inputs hands the caller a file it
            // believes is complete. The warning was already there; the exit code was not.
            return failures > 0 ? 1 : 0;
        });

        return cmd;
    }
}
