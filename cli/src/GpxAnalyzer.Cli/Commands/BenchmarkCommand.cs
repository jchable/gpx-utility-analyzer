using System.CommandLine;
using System.Diagnostics;
using GpxAnalyzer.Cli.Core.Benchmark;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class BenchmarkCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<string>("file") { Description = "GPX file to benchmark" };
        var outputOpt = new Option<string>("--output", "-o") { Description = "CSV output file", DefaultValueFactory = _ => "" };
        var fullOpt = new Option<bool>("--full") { Description = "Full Cartesian product (~1248 runs)", DefaultValueFactory = _ => false };
        var varyOpt = new Option<string>("--vary") { Description = "Axes to vary (comma-separated: preset,elev-algo,elev-smoothing,track-smoothing,dem,elev-params)", DefaultValueFactory = _ => "" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Print progress to stderr", DefaultValueFactory = _ => false };
        var sortOpt = new Option<string>("--sort") { Description = $"Sort by column ({string.Join(", ", BenchmarkOutput.ValidSortColumns)})", DefaultValueFactory = _ => "" };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var demDirOpt = new Option<string>("--dem-dir") { Description = "SRTM .hgt directory", DefaultValueFactory = _ => "" };
        var demCacheOpt = new Option<string>("--dem-cache") { Description = "DEM cache directory", DefaultValueFactory = _ => "" };
        var demAutoOpt = new Option<bool>("--dem-auto-download") { Description = "Auto-download missing tiles", DefaultValueFactory = _ => true };
        var demMaxMemOpt = new Option<int>("--dem-max-memory") { Description = "Max memory for DEM (MB, 0=unlimited)", DefaultValueFactory = _ => 0 };
        var demSkipValOpt = new Option<bool>("--dem-skip-validation") { Description = "Skip tile validation", DefaultValueFactory = _ => false };

        var cmd = new Command("benchmark", "Run multi-configuration benchmark on a GPX file")
        {
            fileArg, outputOpt, fullOpt, varyOpt, verboseOpt, sortOpt,
            maxHrOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt, demSkipValOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var file = parseResult.GetRequiredValue(fileArg);
            var output = parseResult.GetValue(outputOpt) ?? "";
            var full = parseResult.GetValue(fullOpt);
            var vary = parseResult.GetValue(varyOpt) ?? "";
            var verbose = parseResult.GetValue(verboseOpt);
            var sort = parseResult.GetValue(sortOpt) ?? "";
            var maxHr = parseResult.GetValue(maxHrOpt);
            var demDir = parseResult.GetValue(demDirOpt) ?? "";
            var demCache = parseResult.GetValue(demCacheOpt) ?? "";
            var demAuto = parseResult.GetValue(demAutoOpt);
            var demMaxMem = parseResult.GetValue(demMaxMemOpt);
            var demSkipVal = parseResult.GetValue(demSkipValOpt);

            var wallClock = Stopwatch.StartNew();

            GpxDocument doc;
            try
            {
                doc = GpxParser.ParseFile(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing {file}: {ex.Message}");
                return;
            }

            var points = doc.AllPoints();
            if (verbose)
                Console.Error.WriteLine($"Loaded {file}: {points.Count} points, {doc.SegmentCount()} segments");

            // Generate combinations
            List<BenchmarkCombination> combos;
            if (full)
            {
                combos = BenchmarkMatrix.FullMatrix();
            }
            else if (!string.IsNullOrEmpty(vary))
            {
                combos = GenerateVaryCombos(vary);
            }
            else
            {
                combos = BenchmarkMatrix.Reduced();
            }

            if (verbose)
                Console.Error.WriteLine($"Running {combos.Count} configurations...");

            // Check if DEM needed and prepare
            bool needsDem = combos.Any(c => c.UseDem);
            IElevationProvider? demSource = null;
            if (needsDem)
            {
                string cacheDir = string.IsNullOrEmpty(demCache) ? DemSource.DefaultCacheDir() : demCache;
                if (!string.IsNullOrEmpty(demDir) && demAuto)
                    demSource = DemSource.CreateWithCache(demDir, cacheDir, true).WithMaxMemory(demMaxMem).WithSkipValidation(demSkipVal);
                else if (!string.IsNullOrEmpty(demDir))
                    demSource = DemSource.CreateWithCache(demDir, cacheDir, false).WithMaxMemory(demMaxMem).WithSkipValidation(demSkipVal);
                else if (demAuto)
                    demSource = DemSource.CreateAuto(cacheDir).WithMaxMemory(demMaxMem).WithSkipValidation(demSkipVal);

                if (demSource is IElevationPreloader preloader)
                {
                    if (verbose)
                        Console.Error.Write("Preloading DEM tiles...");
                    preloader.PreloadAsync(points).GetAwaiter().GetResult();
                    if (verbose)
                        Console.Error.WriteLine(" done");
                }
            }

            var runCfg = new RunConfig
            {
                Points = points,
                SegmentCount = doc.SegmentCount(),
                DemSource = demSource,
                MaxHR = maxHr,
                Verbose = verbose,
            };

            var results = BenchmarkRunner.Run(combos, runCfg);
            wallClock.Stop();

            // Sort if requested
            if (!string.IsNullOrEmpty(sort))
            {
                if (!BenchmarkOutput.ValidSortColumns.Contains(sort))
                {
                    Console.Error.WriteLine($"Warning: unknown sort column '{sort}'");
                }
                else
                {
                    BenchmarkOutput.SortResults(results, sort);
                }
            }

            // Output table (use base filename like Go's filepath.Base)
            BenchmarkOutput.WriteTable(Console.Out, results, Path.GetFileName(file), points.Count);

            // Write CSV if requested
            if (!string.IsNullOrEmpty(output))
            {
                try
                {
                    BenchmarkOutput.WriteCsv(output, results);
                    Console.Error.WriteLine($"CSV written to {output}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error writing CSV: {ex.Message}");
                }
            }

            Console.Error.WriteLine($"Wall time: {wallClock.Elapsed.TotalSeconds:F1}s");
        });

        return cmd;
    }

    private static List<BenchmarkCombination> GenerateVaryCombos(string axes)
    {
        var baseCfg = BenchmarkMatrix.DefaultBase();
        var seen = new HashSet<string>();
        var combos = new List<BenchmarkCombination>();

        void Add(BenchmarkCombination c)
        {
            if (seen.Add(c.Label())) combos.Add(c);
        }

        foreach (var axis in axes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (axis.ToLowerInvariant())
            {
                case "preset":
                    foreach (var p in new[] { "hiking", "trail", "cycling" })
                        Add(baseCfg with { Preset = p });
                    break;
                case "elev-algo":
                    foreach (var a in new[] { ElevationAlgo.Threshold, ElevationAlgo.DouglasPeucker, ElevationAlgo.Segments })
                        Add(baseCfg with { ElevAlgo = a });
                    break;
                case "elev-smoothing":
                    foreach (var s in new[] { "none", "light", "medium", "heavy" })
                        Add(baseCfg with { ElevSmoothing = s });
                    break;
                case "track-smoothing":
                    foreach (var s in new[] { "none", "light", "medium", "heavy" })
                        Add(baseCfg with { TrackSmoothing = s });
                    break;
                case "dem":
                    Add(baseCfg with { UseDem = true });
                    Add(baseCfg with { UseDem = false });
                    break;
                case "elev-params":
                    foreach (var t in new[] { 1.0, 2.0, 3.0, 5.0 })
                        Add(baseCfg with { ElevAlgo = ElevationAlgo.Threshold, Threshold = t });
                    foreach (var e in new[] { 1.5, 3.0, 5.0 })
                        Add(baseCfg with { ElevAlgo = ElevationAlgo.DouglasPeucker, DpEpsilon = e });
                    foreach (var l in new[] { 100.0, 200.0, 400.0 })
                    foreach (var d in new[] { 1.0, 2.0 })
                        Add(baseCfg with { ElevAlgo = ElevationAlgo.Segments, SegMinLen = l, SegMaxDev = d });
                    break;
                default:
                    Console.Error.WriteLine($"Warning: unknown axis '{axis}'");
                    break;
            }
        }

        if (combos.Count == 0)
            combos.Add(baseCfg);

        return combos;
    }
}
