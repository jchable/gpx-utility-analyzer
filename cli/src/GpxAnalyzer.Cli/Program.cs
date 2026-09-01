using System.CommandLine;
using GpxAnalyzer.Cli.Commands;

var formatOption = new Option<string>("--format", "-f")
{
    Description = "Output format: text or json",
    DefaultValueFactory = _ => "text",
    Recursive = true,
};

var rootCommand = new RootCommand("Analyze GPX files: distance, elevation, stops, and more");
rootCommand.Options.Add(formatOption);

rootCommand.Subcommands.Add(AnalyzeCommand.Create(formatOption));
rootCommand.Subcommands.Add(SplitCommand.Create(formatOption));
rootCommand.Subcommands.Add(MergeCommand.Create(formatOption));
rootCommand.Subcommands.Add(BenchmarkCommand.Create());

return await rootCommand.Parse(args).InvokeAsync();
