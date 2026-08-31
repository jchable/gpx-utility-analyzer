using GpxAnalyzer.Cli.Commands;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Tests.Characterization;

namespace GpxAnalyzer.Cli.Tests.Commands;

public class AnalyzeCommandTests
{
    // -------------------------------------------------------------------- #88

    [Fact]
    public void ClaimOutputPath_DuplicateBasenamesInDifferentDirs_ProducesDistinctPaths()
    {
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var a = AnalyzeCommand.ClaimOutputPath(
            "out", "morning-run", Path.Combine("tracks", "2023", "morning-run.gpx"), claimed);
        var b = AnalyzeCommand.ClaimOutputPath(
            "out", "morning-run", Path.Combine("tracks", "2024", "morning-run.gpx"), claimed);

        Assert.NotEqual(a, b);
        Assert.Contains("2024", b);
    }

    [Fact]
    public void ClaimOutputPath_ThreeWayCollision_StillProducesDistinctPaths()
    {
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new[]
        {
            AnalyzeCommand.ClaimOutputPath("out", "run", Path.Combine("a", "run.gpx"), claimed),
            AnalyzeCommand.ClaimOutputPath("out", "run", Path.Combine("b", "run.gpx"), claimed),
            AnalyzeCommand.ClaimOutputPath("out", "run", Path.Combine("a", "run.gpx"), claimed),
        };
        Assert.Equal(3, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// End-to-end proof of #88. FileResolver enumerates a directory recursively and
    /// de-duplicates on the ABSOLUTE path, so tracks/2023/morning-run.gpx and
    /// tracks/2024/morning-run.gpx both resolve - and both used to map onto
    /// out/morning-run_processed.gpx. The second write silently won while stderr
    /// reported both as exported.
    /// </summary>
    [Fact]
    public void Analyze_DuplicateBasenamesInDifferentDirs_ExportsBothWithoutClobbering()
    {
        List<int> exportedPointCounts = [];
        string[] exported = [];

        var r = CliRunner.Run(
            new CliOptions
            {
                Arrange = w =>
                {
                    // small.gpx has 5 points, two-segments.gpx has 4: distinct sizes make
                    // a clobbered export impossible to mistake for a correct one.
                    Copy(w, "small.gpx", Path.Combine("tracks", "2023", "morning-run.gpx"));
                    Copy(w, "two-segments.gpx", Path.Combine("tracks", "2024", "morning-run.gpx"));
                },
                Inspect = w =>
                {
                    var outDir = Path.Combine(w, "out");
                    exported = Directory.Exists(outDir)
                        ? [.. Directory.GetFiles(outDir).Select(Path.GetFileName)!]
                        : [];
                    exportedPointCounts = [.. Directory.GetFiles(outDir, "*.gpx")
                        .Select(p => GpxParser.ParseFile(p).AllPoints().Count)
                        .Order()];
                },
            },
            "--format", "json", "analyze", "tracks", "--export", "out",
            "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        // Both inputs were analyzed ...
        Assert.Equal(2, r.StdErr.Split("Exported:").Length - 1);
        // ... and both survived on disk, whole.
        Assert.Equal(2, exported.Length);
        Assert.Equal([4, 5], exportedPointCounts);
    }

    // ------------------------------------------------------------------- #107

    /// <summary>
    /// analyze --format json is the documented feed for gpx-ai-analyzer, so
    /// "analyze corrupt.gpx --format json &gt; stats.json &amp;&amp; gpx-ai-analyzer ..."
    /// used to walk straight through the &amp;&amp; and hand the AI analyzer an empty file.
    /// </summary>
    [Fact]
    public void Analyze_CorruptFile_ExitsNonZeroAndWritesNoStdout()
    {
        var r = CliRunner.Run(
            new CliOptions { Arrange = w => File.WriteAllText(
                Path.Combine(w, "corrupt.gpx"), "<gpx><trk><trkseg><trkpt lat=") },
            "analyze", "corrupt.gpx", "--format", "json", "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("Error analyzing corrupt.gpx", r.StdErr);
        Assert.Equal("", r.StdOut.Trim());
    }

    /// <summary>
    /// A partial failure must still be a failure: the good file is analyzed and printed,
    /// but the exit code has to tell the caller that something did not make it.
    /// </summary>
    [Fact]
    public void Analyze_OneGoodOneCorruptFile_StillAnalysesTheGoodOneButExitsNonZero()
    {
        var r = CliRunner.Run(
            new CliOptions { Arrange = w => File.WriteAllText(
                Path.Combine(w, "corrupt.gpx"), "<gpx><trk><trkseg><trkpt lat=") },
            "analyze", "small.gpx", "corrupt.gpx", "--format", "json",
            "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("\"filename\": \"small.gpx\"", r.StdOut);
        Assert.Contains("Error analyzing corrupt.gpx", r.StdErr);
    }

    [Fact]
    public void Analyze_AllFilesGood_StillExitsZero()
    {
        var r = CliRunner.Run("analyze", "small.gpx", "two-segments.gpx",
            "--format", "json", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("\"filename\": \"small.gpx\"", r.StdOut);
        Assert.Contains("\"filename\": \"two-segments.gpx\"", r.StdOut);
    }

    // --------------------------------------------------------------- helpers

    private static void Copy(string workDir, string seededFixture, string relativeTarget)
    {
        var target = Path.Combine(workDir, relativeTarget);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(workDir, seededFixture), target);
    }
}
