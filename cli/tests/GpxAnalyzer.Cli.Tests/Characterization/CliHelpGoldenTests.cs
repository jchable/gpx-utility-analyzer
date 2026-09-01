namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Golden tests for --help output. Unlike CliGoldenTests these ARE expected to change
/// when System.CommandLine changes major version: the help renderer belongs to the
/// library, not to this codebase. Re-baseline them deliberately (UPDATE_GOLDEN=1) and
/// review the committed diff.
/// </summary>
public class CliHelpGoldenTests
{
    [Theory]
    [InlineData("help-root")]
    [InlineData("help-analyze")]
    [InlineData("help-split")]
    [InlineData("help-merge")]
    [InlineData("help-benchmark")]
    public void Help_MatchesGolden(string name)
    {
        string[] args = name == "help-root"
            ? ["--help"]
            : [name["help-".Length..], "--help"];

        var r = CliRunner.Run(args);
        Assert.Equal(0, r.ExitCode);
        Golden.Verify(name, r.StdOut);
    }
}
