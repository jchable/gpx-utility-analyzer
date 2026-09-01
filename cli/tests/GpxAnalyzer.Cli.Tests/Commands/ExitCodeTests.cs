using GpxAnalyzer.Cli.Tests.Characterization;

namespace GpxAnalyzer.Cli.Tests.Commands;

/// <summary>
/// #139. One rule across every command: a run that could not produce the output it was asked
/// for exits non-zero. #107 established it for `analyze`; `split` still printed its error and
/// then fell through to `return 0`, so `split bad.gpx &amp;&amp; next-step` walked straight
/// through the &amp;&amp;. The command was inconsistent with itself, too - a bad *argument*
/// exited 1 while a bad *file* exited 0.
/// </summary>
public class ExitCodeTests
{
    private const string CorruptGpx = "<gpx><trk><trkseg><trkpt lat=";

    private static CliOptions WithCorruptFile(Action<string>? inspect = null) => new()
    {
        Arrange = w => File.WriteAllText(Path.Combine(w, "corrupt.gpx"), CorruptGpx),
        Inspect = inspect,
    };

    // ------------------------------------------------------------------------- split

    [Fact]
    public void Split_MissingFile_ExitsNonZeroAndWritesNothing()
    {
        string[] produced = [];
        var r = CliRunner.Run(
            new CliOptions
            {
                Inspect = w => produced = Directory.Exists(Path.Combine(w, "out"))
                    ? Directory.GetFiles(Path.Combine(w, "out"))
                    : [],
            },
            "split", "missing.gpx", "--output-dir", "out", "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("Error:", r.StdErr);
        Assert.Empty(produced);
    }

    [Fact]
    public void Split_CorruptFile_ExitsNonZero()
    {
        var r = CliRunner.Run(WithCorruptFile(),
            "split", "corrupt.gpx", "--output-dir", "out", "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("Error:", r.StdErr);
        Assert.DoesNotContain("Split into", r.StdErr);
    }

    /// <summary>
    /// A per-segment failure is still a failure. The output path is occupied by a directory,
    /// so writing the split file throws inside the per-segment try/catch.
    /// </summary>
    [Fact]
    public void Split_SegmentThatCannotBeWritten_ExitsNonZero()
    {
        var r = CliRunner.Run(
            new CliOptions
            {
                Arrange = w => Directory.CreateDirectory(Path.Combine(w, "out", "segment-001.gpx")),
            },
            "split", "small.gpx", "--interval", "30m", "--output-dir", "out",
            "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("Error processing segment 1", r.StdErr);
    }

    [Fact]
    public void Split_GoodFile_StillExitsZero()
    {
        var r = CliRunner.Run("--format", "json", "split", "small.gpx",
            "--interval", "30m", "--output-dir", "out", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Split into", r.StdErr);
    }

    // ------------------------------------------------------------------------- merge

    /// <summary>
    /// A merge that silently dropped one of its inputs produced a file the caller believes is
    /// complete. The warning was there; the exit code was not.
    /// </summary>
    [Fact]
    public void Merge_OneGoodOneCorruptFile_MergesTheGoodOneButExitsNonZero()
    {
        string[] written = [];
        var r = CliRunner.Run(
            WithCorruptFile(inspect: w => written = Directory.Exists(Path.Combine(w, "out"))
                ? Directory.GetFiles(Path.Combine(w, "out"))
                : []),
            "merge", "small.gpx", "corrupt.gpx", "-o", "out/merged.gpx",
            "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("failed to parse corrupt.gpx", r.StdErr);
        Assert.Single(written);   // what could be merged still was
    }

    [Fact]
    public void Merge_AllFilesGood_StillExitsZero()
    {
        var r = CliRunner.Run("merge", "small.gpx", "two-segments.gpx",
            "-o", "out/merged.gpx", "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
    }

    // --------------------------------------------------------------------- benchmark

    /// <summary>
    /// `--vary` naming nothing the CLI recognises used to warn and then run the default base
    /// combination, presenting one row of a matrix the user never asked for as if it were the
    /// answer.
    /// </summary>
    [Fact]
    public void Benchmark_VaryWithNoRecognisedAxis_ExitsNonZeroWithoutATable()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--vary", "bogus",
            "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("bogus", r.StdErr);
        Assert.Equal("", r.StdOut.Trim());
    }

    [Fact]
    public void Benchmark_VaryWithARecognisedAxis_StillExitsZero()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--vary", "preset",
            "--dem-auto-download", "false");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Configurations: 3", r.StdOut);
    }
}
