using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Input;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var filesArg = new Argument<string[]>("files", "GPX files, directories, or glob patterns")
        {
            Arity = ArgumentArity.OneOrMore
        };
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
        var exportOpt = new Option<string>("--export", () => "", "Export preprocessed GPX directory");
        var enrichOpt = new Option<bool>("--enrich", () => false, "Include computed metrics in export");
        var maxHrOpt = new Option<int>("--max-hr", () => 0, "Max HR for zone calculation");
        var maxSpeedOpt = new Option<double>("--max-speed", () => 0, "GPS outlier removal threshold (m/s)");
        var fixAnomaliesOpt = new Option<bool>("--fix-anomalies", () => false, "Apply automatic anomaly corrections");

        var cmd = new Command("analyze", "Analyze GPX files: distance, elevation, speed, pace, stops")
        {
            filesArg, presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, exportOpt, enrichOpt, maxHrOpt, maxSpeedOpt, fixAnomaliesOpt
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var files = ctx.ParseResult.GetValueForArgument(filesArg);
            var format = ctx.ParseResult.GetValueForOption(formatOption) ?? "text";
            var export = ctx.ParseResult.GetValueForOption(exportOpt) ?? "";
            var enrich = ctx.ParseResult.GetValueForOption(enrichOpt);

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var resolvedFiles = FileResolver.ResolveFiles(files);
            var cfg = SharedFlags.BuildConfigFromContext(ctx, presetOpt, stopSpeedOpt, stopDurationOpt,
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
