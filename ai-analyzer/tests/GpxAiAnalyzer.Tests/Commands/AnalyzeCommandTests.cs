using System.CommandLine;
using GpxAiAnalyzer.Commands;
using GpxAiAnalyzer.Core.Providers;

namespace GpxAiAnalyzer.Tests.Commands;

public class AnalyzeCommandTests
{
    [Fact]
    public async Task Analyze_WithNoInputAndNoStdin_ExitsNonZero()
    {
        // The test host always runs with a redirected stdin, so the missing-input
        // branch is reached through the injectable probe instead. This is exactly
        // the CI case where the upstream gpx-analyzer step was omitted.
        var root = new RootCommand
        {
            AnalyzeCommand.Create(new ProviderRegistry(), isInputRedirected: () => false),
        };

        var stderr = new StringWriter();
        var previousError = Console.Error;
        Console.SetError(stderr);
        try
        {
            var exitCode = await root.Parse(["analyze", "--provider", "openai"]).InvokeAsync();

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Console.SetError(previousError);
        }

        Assert.Contains("provide --input file or pipe JSON via stdin", stderr.ToString());
    }
}
