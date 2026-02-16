using System.CommandLine;
using GpxAnalyzer.Cli.Commands;

var formatOption = new Option<string>("--format", () => "text", "Output format: text or json");
formatOption.AddAlias("-f");

var rootCommand = new RootCommand("Analyze GPX files: distance, elevation, stops, and more");
rootCommand.AddGlobalOption(formatOption);

rootCommand.AddCommand(AnalyzeCommand.Create(formatOption));
rootCommand.AddCommand(SplitCommand.Create(formatOption));
rootCommand.AddCommand(MergeCommand.Create(formatOption));
rootCommand.AddCommand(BenchmarkCommand.Create());

return await rootCommand.InvokeAsync(args);
