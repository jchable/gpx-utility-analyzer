using System.Text.Json;
using System.Text.RegularExpressions;

namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Pins the OBSERVABLE EFFECT of the twelve option defaults that nothing else covers.
///
/// The help goldens in <see cref="CliHelpGoldenTests"/> currently record these defaults as
/// "[default: False]" / "[]" text, but System.CommandLine 2.x stops printing defaults for
/// booleans and empty strings and those goldens get re-baselined during the migration - so
/// after the migration the help text pins nothing. A default silently mis-transcribed while
/// rewriting 72 option declarations is the most likely migration bug, and these tests are
/// what would catch it.
///
/// Each test therefore drives the built executable and asserts on what the default MAKES THE
/// CLI DO, never on how the option is declared: no System.CommandLine type and no
/// command-layer API is referenced anywhere, so every test here is expected to pass
/// unchanged before and after the migration.
///
/// Each test also runs a control invocation that passes the option explicitly, proving the
/// assertion actually discriminates rather than holding vacuously.
///
/// No test may reach the network. Every invocation either passes --dem-auto-download false
/// or - where that flag's own default is the subject - blocks downloads on the filesystem
/// (see <see cref="DemFixture.CreateDownloadBlockingCache"/>).
/// </summary>
public class CliDefaultsTests
{
    /// <summary>What the working directory holds when the CLI has written nothing.</summary>
    private static IReadOnlyList<string> Fixtures => CliRunner.SeededFixtures;

    // ---------------------------------------------------------------- analyze --enrich

    [Fact]
    public void Analyze_EnrichDefaultsFalse_ExportedGpxCarriesNoMetricExtensions()
    {
        string? plain = null;
        var r = CliRunner.Run(
            new CliOptions { Inspect = w => plain = File.ReadAllText(ExportedGpx(w)) },
            "analyze", "--dem-auto-download", "false", "--export", "exported", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.NotNull(plain);

        // It is a real, complete GPX - the assertions below are about what is missing from a
        // file that was genuinely written, not about an empty one.
        Assert.Contains("<trkpt lat=\"48.8566\" lon=\"2.3522\">", plain);
        Assert.Contains("<ele>", plain);

        Assert.DoesNotContain("TrackPointMetrics", plain);
        Assert.DoesNotContain("gpx-analyzer.io/extensions/v1", plain);
        Assert.DoesNotContain("<extensions>", plain);

        // Control: --enrich is what adds them, so the absence above is meaningful.
        string? enriched = null;
        var control = CliRunner.Run(
            new CliOptions { Inspect = w => enriched = File.ReadAllText(ExportedGpx(w)) },
            "analyze", "--dem-auto-download", "false", "--export", "exported", "--enrich", "small.gpx");

        Assert.Equal(0, control.ExitCode);
        Assert.Contains("gpxa:TrackPointMetrics", enriched);

        static string ExportedGpx(string workDir) =>
            Path.Combine(workDir, "exported", "small_processed.gpx");
    }

    // ---------------------------------------------------------------- analyze --export

    [Fact]
    public void Analyze_ExportDefaultsEmpty_WritesNothingToDisk()
    {
        string[] entries = [];
        var r = CliRunner.Run(
            new CliOptions { Inspect = w => entries = [.. Directory.GetFileSystemEntries(w).Select(Path.GetFileName)!] },
            "--format", "json", "analyze", "--dem-auto-download", "false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("Exported:", r.StdErr);
        // Nothing beyond the fixtures the runner seeded the directory with.
        Assert.Equal(Fixtures, entries.OrderBy(e => e, StringComparer.Ordinal));

        // Control: a non-empty --export does create the directory and the file.
        string[] withExport = [];
        var control = CliRunner.Run(
            new CliOptions { Inspect = w => withExport = [.. Directory.GetFileSystemEntries(w).Select(Path.GetFileName)!] },
            "--format", "json", "analyze", "--dem-auto-download", "false", "--export", "exported", "small.gpx");

        Assert.Equal(0, control.ExitCode);
        Assert.Contains("exported", withExport);
    }

    // ---------------------------------------------------------------- analyze --fix-anomalies

    [Fact]
    public void Analyze_FixAnomaliesDefaultsFalse_ReportsAnomaliesWithoutCorrectingThem()
    {
        var r = CliRunner.Run("-f", "json", "analyze", "--dem-auto-download", "false",
            "with-gps-quality.gpx");

        Assert.Equal(0, r.ExitCode);
        var anomalies = Json(r.StdOut).GetProperty("anomalies");

        // Detection still runs...
        Assert.Equal(2, anomalies.GetProperty("total_count").GetInt32());
        // ... but nothing is corrected.
        Assert.False(anomalies.GetProperty("correction_applied").GetBoolean());
        Assert.All(anomalies.GetProperty("anomalies").EnumerateArray(),
            a => Assert.False(a.GetProperty("was_corrected").GetBoolean()));

        // Control: the flag flips correction_applied, so the assertion above is not vacuous.
        var control = CliRunner.Run("-f", "json", "analyze", "--dem-auto-download", "false",
            "--fix-anomalies", "with-gps-quality.gpx");
        Assert.True(Json(control.StdOut).GetProperty("anomalies")
            .GetProperty("correction_applied").GetBoolean());
    }

    // ---------------------------------------------------------------- merge --analyze

    [Fact]
    public void Merge_AnalyzeDefaultsFalse_PrintsNoStatisticsBlock()
    {
        var r = CliRunner.Run("--format", "json", "merge", "small.gpx", "two-segments.gpx",
            "-o", "out/merged.gpx", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        // The merge itself happened - only the statistics block is absent.
        Assert.Contains("Merged 2 files ->", r.StdErr);
        Assert.Equal("", r.StdOut.Trim());

        // Control: --analyze is what produces the block.
        var control = CliRunner.Run("--format", "json", "merge", "small.gpx", "two-segments.gpx",
            "-o", "out/merged.gpx", "--analyze", "--dem-auto-download", "false");
        Assert.Contains("total_distance_m", control.StdOut);
    }

    // ---------------------------------------------------------------- benchmark --verbose

    [Fact]
    public void Benchmark_VerboseDefaultsFalse_PrintsNoProgressToStderr()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        // The only thing an unverbose benchmark says on stderr is the wall time.
        Assert.Matches(@"^Wall time: \d+[.,]\d+s$", r.StdErr.Trim());

        // Control: -v adds the progress lines the default suppresses.
        var control = CliRunner.Run("benchmark", "small.gpx", "-v", "--dem-auto-download", "false");
        Assert.Contains("Loaded small.gpx", control.StdErr);
        Assert.Contains("Running 22 configurations...", control.StdErr);
    }

    // ---------------------------------------------------------------- benchmark --vary

    [Fact]
    public void Benchmark_VaryDefaultsEmpty_RunsTheReducedMatrix()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Configurations: 22", r.StdOut);
        // Golden pins the whole reduced matrix, its contents AND its order.
        Golden.Verify("benchmark-defaults", Golden.NormalizeBenchmark(r.StdOut));

        // Control: a non-empty --vary selects a different, smaller matrix.
        var control = CliRunner.Run("benchmark", "small.gpx", "--vary", "preset",
            "--dem-auto-download", "false");
        Assert.Contains("Configurations: 3", control.StdOut);
    }

    // ---------------------------------------------------------------- benchmark --sort

    [Fact]
    public void Benchmark_SortDefaultsEmpty_LeavesRowsInMatrixOrder()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        var rows = Golden.NormalizeBenchmark(r.StdOut);

        // Row 1 is the matrix's own first combination. Under --sort elev-gain the first row
        // is instead the t=5.0 / +0 m one - see the benchmark-reduced golden.
        Assert.Contains("\n 1 hiking threshold medium none yes t=2.0 ", rows);

        var gains = ElevationGains(rows);
        Assert.Equal(22, gains.Count);
        Assert.False(gains.SequenceEqual(gains.Order()),
            "rows came out in ascending elevation-gain order: --sort no longer defaults to \"\"");

        // Control: an explicit --sort does reorder them.
        var control = CliRunner.Run("benchmark", "small.gpx", "--sort", "elev-gain",
            "--dem-auto-download", "false");
        var sorted = ElevationGains(Golden.NormalizeBenchmark(control.StdOut));
        Assert.True(sorted.SequenceEqual(sorted.Order()));
    }

    // ---------------------------------------------------------------- benchmark --output

    [Fact]
    public void Benchmark_OutputDefaultsEmpty_WritesNoCsv()
    {
        string[] entries = [];
        var r = CliRunner.Run(
            new CliOptions { Inspect = w => entries = [.. Directory.GetFileSystemEntries(w).Select(Path.GetFileName)!] },
            "benchmark", "small.gpx", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("CSV written to", r.StdErr);
        Assert.Equal(Fixtures, entries.OrderBy(e => e, StringComparer.Ordinal));

        // Control: a non-empty --output writes the CSV and says so.
        string[] withCsv = [];
        var control = CliRunner.Run(
            new CliOptions { Inspect = w => withCsv = [.. Directory.GetFileSystemEntries(w).Select(Path.GetFileName)!] },
            "benchmark", "small.gpx", "-o", "results.csv", "--dem-auto-download", "false");

        Assert.Contains("CSV written to results.csv", control.StdErr);
        Assert.Contains("results.csv", withCsv);
    }

    // ---------------------------------------------------------------- --dem-dir

    [Fact]
    public void DemDirDefaultsEmpty_SoNoDemSourceIsBuilt()
    {
        // With --dem-auto-download false, a DEM source exists only if --dem-dir is non-empty.
        // No source at all means no tile lookup, hence not even a "tile not available" warning.
        var r = CliRunner.Run(DemFixture.Offline(), "-f", "json", "analyze",
            "--dem-auto-download", "false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("DEM tile", r.StdErr);
        Assert.Equal(GpsMaxElevation, MaxElevation(r.StdOut), 6);

        // Control 1: any non-empty --dem-dir does build a source, which then complains.
        var missing = CliRunner.Run(
            DemFixture.Offline(w => Directory.CreateDirectory(Path.Combine(w, "dem"))),
            "-f", "json", "analyze", "--dem-dir", "dem", "--dem-auto-download", "false", "small.gpx");
        Assert.Contains(DemFixture.MissingTileWarning, missing.StdErr);

        // Control 2: and a populated one visibly rewrites the elevations.
        var applied = CliRunner.Run(
            DemFixture.Offline(w => DemFixture.WriteValidTile(
                Path.Combine(w, "dem", DemFixture.TileKey + ".hgt"))),
            "-f", "json", "analyze", "--dem-dir", "dem", "--dem-auto-download", "false", "small.gpx");
        Assert.Equal(DemFixture.TileElevation, MaxElevation(applied.StdOut), 6);
    }

    // ---------------------------------------------------------------- --dem-cache

    [Fact]
    public void DemCacheDefaultsEmpty_SoTheCacheFallsBackToThePlatformDirectory()
    {
        // The tile exists ONLY in the (sandboxed) platform cache directory. An empty
        // --dem-cache is what makes the CLI look there; any other default would miss it.
        var r = CliRunner.Run(
            DemFixture.Offline(w =>
            {
                Directory.CreateDirectory(Path.Combine(w, "dem"));
                foreach (var tile in DemFixture.DefaultCacheTilePaths(DemFixture.HomeIn(w)))
                    DemFixture.WriteValidTile(tile);
            }),
            "-f", "json", "analyze", "--dem-dir", "dem", "--dem-auto-download", "false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("DEM tile", r.StdErr);
        Assert.Equal(DemFixture.TileElevation, MaxElevation(r.StdOut), 6);

        // Control: point --dem-cache anywhere else and the very same tile stops being found,
        // which is what would happen for the whole run if the default were not empty.
        var elsewhere = CliRunner.Run(
            DemFixture.Offline(w =>
            {
                Directory.CreateDirectory(Path.Combine(w, "dem"));
                Directory.CreateDirectory(Path.Combine(w, "other-cache"));
                foreach (var tile in DemFixture.DefaultCacheTilePaths(DemFixture.HomeIn(w)))
                    DemFixture.WriteValidTile(tile);
            }),
            "-f", "json", "analyze", "--dem-dir", "dem", "--dem-cache", "other-cache",
            "--dem-auto-download", "false", "small.gpx");

        Assert.Contains(DemFixture.MissingTileWarning, elsewhere.StdErr);
        Assert.Equal(GpsMaxElevation, MaxElevation(elsewhere.StdOut), 6);
    }

    // ---------------------------------------------------------------- --dem-skip-validation

    [Fact]
    public void DemSkipValidationDefaultsFalse_SoAnUnvalidatableTileIsRejected()
    {
        Action<string> arrangeTile = w => DemFixture.WriteTileThatFailsValidation(
            Path.Combine(w, "dem", DemFixture.TileKey + ".hgt"));

        var r = CliRunner.Run(DemFixture.Offline(arrangeTile), "-f", "json", "analyze",
            "--dem-dir", "dem", "--dem-auto-download", "false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        // Validation ran and threw the tile away, so the GPS elevations survive.
        Assert.Equal(GpsMaxElevation, MaxElevation(r.StdOut), 6);

        // Control: skipping validation accepts the very same tile and its elevations win.
        var control = CliRunner.Run(DemFixture.Offline(arrangeTile), "-f", "json", "analyze",
            "--dem-dir", "dem", "--dem-skip-validation", "true",
            "--dem-auto-download", "false", "small.gpx");
        Assert.Equal(DemFixture.TileElevation, MaxElevation(control.StdOut), 6);
    }

    // ---------------------------------------------------------------- --dem-auto-download

    /// <summary>
    /// The awkward one: this default is TRUE, and being true is precisely what makes the CLI
    /// go to the network - which is why every other test passes it explicitly as false.
    ///
    /// It is still pinnable offline. With --dem-dir left empty, a DEM source is constructed
    /// *only if* auto-download is on, and the source announces itself by warning about the
    /// tile it could not get. Downloading is prevented structurally rather than by timeout:
    /// --dem-cache points at a directory where the tile's shard name is already taken by a
    /// regular file, and TileDownloader.DownloadTileAsync calls Directory.CreateDirectory on
    /// that path before it constructs an HttpClient, so it throws before any socket is opened.
    /// The test asserts no tile was fetched, so it fails loudly rather than passing quietly if
    /// that ever stops holding.
    /// </summary>
    [Theory]
    [InlineData("analyze")]
    [InlineData("split")]
    [InlineData("merge")]
    [InlineData("benchmark")]
    public void DemAutoDownloadDefaultsTrue_SoADemSourceIsBuiltWithoutADemDir(string command)
    {
        string[] tail = command switch
        {
            "analyze" => ["analyze", "small.gpx"],
            "split" => ["split", "small.gpx", "--interval", "30m", "--output-dir", "out"],
            "merge" => ["merge", "small.gpx", "-o", "out/merged.gpx", "--analyze"],
            _ => ["benchmark", "small.gpx"],
        };

        var downloaded = new List<string>();
        var options = DemFixture.Offline(
            arrange: w => DemFixture.CreateDownloadBlockingCache(w),
            inspect: w => downloaded.AddRange(
                Directory.GetFiles(w, "*.hgt", SearchOption.AllDirectories)));

        var withDefault = CliRunner.Run(options,
            [.. tail, "--dem-cache", "blocked-cache"]);

        Assert.Equal(0, withDefault.ExitCode);
        Assert.Contains(DemFixture.MissingTileWarning, withDefault.StdErr);
        // Proof that the blocker held and nothing was actually fetched.
        Assert.Empty(downloaded);

        // Control: turn it off and no source is built at all, so the warning disappears.
        var withFalse = CliRunner.Run(options,
            [.. tail, "--dem-cache", "blocked-cache", "--dem-auto-download", "false"]);

        Assert.Equal(0, withFalse.ExitCode);
        Assert.DoesNotContain("DEM tile", withFalse.StdErr);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Max elevation small.gpx reports when no DEM correction is applied.</summary>
    private const double GpsMaxElevation = 43.333333333333336;

    private static List<int> ElevationGains(string normalizedTable) =>
        [.. Regex.Matches(normalizedTable, @"^ \d+ .* \+(\d+) m ", RegexOptions.Multiline)
            .Select(m => int.Parse(m.Groups[1].Value))];

    private static JsonElement Json(string stdout) => JsonDocument.Parse(stdout).RootElement;

    private static double MaxElevation(string stdout) =>
        Json(stdout).GetProperty("max_elevation_m").GetDouble();
}
