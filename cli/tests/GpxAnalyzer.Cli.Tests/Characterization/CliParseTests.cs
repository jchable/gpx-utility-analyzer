namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Pins parse-level behaviours that no golden covers: what happens to an option the CLI does
/// not know, to a --format value it cannot honour, and to the "--option=value" spelling the
/// published docs tell users to type.
///
/// Where the wording belongs to System.CommandLine these tests deliberately assert on the
/// contract (non-zero exit, the offending token named) rather than the exact sentence, since
/// the library may reword it across the major version. Where the wording is this codebase's
/// own it is asserted verbatim.
/// </summary>
public class CliParseTests
{
    [Fact]
    public void UnknownOption_OnACommandWithASingleFileArgument_IsRejected()
    {
        // benchmark takes exactly one file, so --bogus cannot be swallowed as an argument
        // and is reported as unrecognised. Current wording:
        //   Unrecognized command or argument '--bogus'.
        var r = CliRunner.Run("benchmark", "small.gpx", "--bogus", "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("--bogus", r.StdOut + r.StdErr);
    }

    /// <summary>
    /// #136. `analyze`, `merge` and `split` all take file arguments, so an unknown option is
    /// bound to the argument as a VALUE rather than rejected by the parser - and the resolver
    /// it reached then threw straight out of the handler, printing a stack trace.
    ///
    /// This test previously pinned that behaviour, asserting the raw resolver message
    /// "--bogus is not a .gpx file"; it is updated deliberately. The contract is now: one
    /// line, naming the offending token, and never a stack trace.
    /// </summary>
    [Theory]
    [InlineData("analyze")]
    [InlineData("merge")]
    [InlineData("split")]
    public void UnknownOption_OnAFileTakingCommand_IsOneLineAndNeverAStackTrace(string command)
    {
        var r = CliRunner.Run(command, "--bogus", "--dem-auto-download", "false");
        var output = r.StdOut + r.StdErr;

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("--bogus", output);
        Assert.DoesNotContain("Unhandled exception", output);
        Assert.DoesNotContain("   at ", output);
    }

    /// <summary>
    /// A mistyped FILENAME must stay distinguishable from a mistyped option: same exit code,
    /// different diagnostic.
    /// </summary>
    [Theory]
    [InlineData("analyze")]
    [InlineData("merge")]
    [InlineData("split")]
    public void MissingFile_IsReportedAsAMissingFileNotAsAnUnknownOption(string command)
    {
        var r = CliRunner.Run(command, "missing.gpx", "--dem-auto-download", "false");
        var output = r.StdOut + r.StdErr;

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("missing.gpx", output);
        Assert.DoesNotContain("unrecognized option", output);
        Assert.DoesNotContain("Unhandled exception", output);
    }

    /// <summary>
    /// A file argument that is not a GPX at all is a third, distinct case, and it is this
    /// codebase's own message - pinned verbatim.
    /// </summary>
    [Theory]
    [InlineData("analyze")]
    [InlineData("merge")]
    public void NonGpxFileArgument_KeepsItsOwnDiagnostic(string command)
    {
        var r = CliRunner.Run(
            new CliOptions { Arrange = w => File.WriteAllText(Path.Combine(w, "notes.txt"), "x") },
            command, "notes.txt", "--dem-auto-download", "false");
        var output = r.StdOut + r.StdErr;

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("notes.txt is not a .gpx file", output);
        Assert.DoesNotContain("Unhandled exception", output);
    }

    [Fact]
    public void InvalidFormatValue_IsRejectedByTheFormatterFactory()
    {
        // --format is a plain string option with no accepted-value list, so an unusable value
        // survives parsing and blows up in FormatterFactory. Message owned by this codebase.
        var r = CliRunner.Run("--format", "xml", "analyze", "--dem-auto-download", "false",
            "small.gpx");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("Unknown format 'xml', expected 'text' or 'json'", r.StdOut + r.StdErr);
        Assert.Equal("", r.StdOut.Trim());
    }

    /// <summary>
    /// docs/content/cli/{benchmark,elevation,recipes}.md all tell users to write
    /// --dem-auto-download=false. The equals spelling currently works and exits 0; nothing
    /// else in the suite would notice if it stopped, because every other test uses the
    /// space-separated form.
    /// </summary>
    [Theory]
    [InlineData("analyze")]
    [InlineData("benchmark")]
    public void EqualsForm_OfDemAutoDownload_IsAcceptedAndTurnsTheFlagOff(string command)
    {
        // Run inside the offline sandbox: were the equals form ever misparsed as "true" this
        // would build an auto-downloading DEM source, and the blocked cache is what stops that
        // from becoming a real network call. It also gives the assertion its teeth - a source
        // that got built would announce itself with the missing-tile warning.
        var options = new CliOptions { Arrange = w => DemFixture.CreateDownloadBlockingCache(w) };
        var r = CliRunner.Run(options, command, "small.gpx",
            "--dem-cache", "blocked-cache", "--dem-auto-download=false");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("DEM tile", r.StdErr);
        Assert.NotEqual("", r.StdOut.Trim());
    }

    [Fact]
    public void EqualsForm_OfAGlobalStringOption_IsAcceptedToo()
    {
        var options = new CliOptions { Arrange = w => DemFixture.CreateDownloadBlockingCache(w) };
        var r = CliRunner.Run(options, "--format=json", "analyze",
            "--dem-cache", "blocked-cache", "--dem-auto-download=false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("DEM tile", r.StdErr);
        Assert.StartsWith("{", r.StdOut.TrimStart());
        Assert.Contains("\"filename\": \"small.gpx\"", r.StdOut);
    }
}
