namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Characterization tests: they pin the CURRENT stdout of the CLI command layer
/// byte-for-byte. They exist to fence the System.CommandLine 2.x migration and must
/// keep passing unchanged across it. If one of them fails after a command-layer edit,
/// the edit changed behaviour.
///
/// Every invocation passes --dem-auto-download false: the default is true, which
/// downloads SRTM tiles over the network.
/// </summary>
public class CliGoldenTests
{
    [Fact]
    public void Analyze_JsonDefaults_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "analyze", "--dem-auto-download", "false", "small.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-default", r.StdOut);
    }

    [Fact]
    public void Analyze_TextFormatter_MatchesGolden()
    {
        var r = CliRunner.Run("analyze", "--dem-auto-download", "false", "small.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-text", r.StdOut);
    }

    [Fact]
    public void Analyze_ManyFlagsAndGlobalOptionAfterSubcommand_MatchesGolden()
    {
        var r = CliRunner.Run(
            "analyze", "--format", "json", "--dem-auto-download", "false",
            "--preset", "trail", "--smoothing", "heavy", "--track-smoothing", "light",
            "--elevation-algo", "douglas-peucker", "--dp-epsilon", "1.5",
            "--elevation-threshold", "1", "--max-hr", "190", "--max-speed", "8",
            "with-extensions.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-flags", r.StdOut);
    }

    [Fact]
    public void Analyze_SegmentsAlgoAndStopOverrides_MatchesGolden()
    {
        var r = CliRunner.Run(
            "--format", "json", "analyze", "--dem-auto-download", "false",
            "--elevation-algo", "segments", "--seg-min-length", "100", "--seg-max-deviation", "1",
            "--stop-speed", "0.5", "--stop-duration", "30",
            "two-segments.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-segments", r.StdOut);
    }

    [Fact]
    public void Analyze_ShortFormatAliasAndFixAnomalies_MatchesGolden()
    {
        var r = CliRunner.Run("-f", "json", "analyze", "--dem-auto-download", "false",
            "--fix-anomalies", "with-gps-quality.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-fix-anomalies", r.StdOut);
    }

    [Fact]
    public void Analyze_ExportEnriched_MatchesGoldenAndReportsExport()
    {
        var r = CliRunner.Run("--format", "json", "analyze", "--dem-auto-download", "false",
            "--export", "exported", "--enrich", "small.gpx");
        Assert.Equal(0, r.ExitCode);
        // The export path is built with Path.Combine, so it is platform-dependent:
        // assert on the invariant part only, and golden just the stdout.
        Assert.Contains("_processed.gpx", r.StdErr);
        Golden.Verify("analyze-export", r.StdOut);
    }

    [Fact]
    public void Split_TwelveHours_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "split", "two-segments.gpx",
            "--interval", "12h", "--output-dir", "out", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("split-12h", r.StdOut);
    }

    [Fact]
    public void Split_ThirtyMinutesWithPrefix_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "split", "small.gpx",
            "--interval", "30m", "--output-dir", "out2", "--prefix", "chunk",
            "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("split-30m", r.StdOut);
    }

    [Fact]
    public void Merge_WithAnalyze_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "merge", "small.gpx", "two-segments.gpx",
            "--output", "out/merged.gpx", "--analyze", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("merge-analyze", r.StdOut);
    }

    [Fact]
    public void Merge_ShortOutputAliasAndNoSort_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "merge", "small.gpx", "two-segments.gpx",
            "-o", "out/m2.gpx", "--analyze", "--sort", "false", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("merge-nosort", r.StdOut);
    }

    [Fact]
    public void Benchmark_VaryPreset_MatchesGolden()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--vary", "preset",
            "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("benchmark-vary-preset", Golden.NormalizeBenchmark(r.StdOut));
    }

    [Fact]
    public void Benchmark_ReducedSortedVerbose_MatchesGolden()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--sort", "elev-gain", "-v",
            "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Running", r.StdErr);   // -v alias reached the handler
        Golden.Verify("benchmark-reduced", Golden.NormalizeBenchmark(r.StdOut));
    }

    [Fact]
    public void Analyze_MissingRequiredArgument_ExitsOneAndReportsIt()
    {
        var r = CliRunner.Run("analyze");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Required argument missing for command: 'analyze'.", r.StdOut + r.StdErr);
    }
}
