using System.CommandLine;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Input;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var filesArg = new Argument<string[]>("files")
        {
            Description = "GPX files, directories, or glob patterns",
            Arity = ArgumentArity.OneOrMore
        };
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
        var exportOpt = new Option<string>("--export") { Description = "Export preprocessed GPX directory", DefaultValueFactory = _ => "" };
        var enrichOpt = new Option<bool>("--enrich") { Description = "Include computed metrics in export", DefaultValueFactory = _ => false };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var maxSpeedOpt = new Option<double>("--max-speed") { Description = "GPS outlier removal threshold (m/s)", DefaultValueFactory = _ => 0 };
        var fixAnomaliesOpt = new Option<bool>("--fix-anomalies") { Description = "Apply automatic anomaly corrections", DefaultValueFactory = _ => false };

        var cmd = new Command("analyze", "Analyze GPX files: distance, elevation, speed, pace, stops")
        {
            filesArg, presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, exportOpt, enrichOpt, maxHrOpt, maxSpeedOpt, fixAnomaliesOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var files = parseResult.GetRequiredValue(filesArg);
            var format = parseResult.GetValue(formatOption) ?? "text";
            var export = parseResult.GetValue(exportOpt) ?? "";
            var enrich = parseResult.GetValue(enrichOpt);

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var resolvedFiles = FileResolver.ResolveFiles(files);
            var cfg = SharedFlags.BuildConfigFromParseResult(parseResult, presetOpt, stopSpeedOpt, stopDurationOpt,
                elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                maxHrOpt, maxSpeedOpt, fixAnomaliesOpt);

            foreach (var path in resolvedFiles)
            {
                try
                {
                    AnalyzeFile(path, formatter, cfg, export, enrich);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error analyzing {path}: {ex.Message}");
                }
            }
        });

        return cmd;
    }

    private static void AnalyzeFile(string path, IFormatter formatter, ComputeConfig cfg,
        string exportDir, bool enrich)
    {
        var doc = GpxParser.ParseFile(path);
        var points = doc.AllPoints();
        var (summary, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);
        formatter.Format(Console.Out, path, summary, cfg.StopConfig);

        if (!string.IsNullOrEmpty(exportDir))
        {
            string baseName = Path.GetFileNameWithoutExtension(path);
            string outPath = Path.Combine(exportDir, baseName + "_processed.gpx");
            Directory.CreateDirectory(exportDir);

            if (enrich)
                GpxWriter.WriteEnriched(outPath, processed, baseName);
            else
                GpxWriter.Write(outPath, processed, baseName);

            Console.Error.WriteLine($"Exported: {outPath} ({processed.Count} points)");
        }
    }
}
