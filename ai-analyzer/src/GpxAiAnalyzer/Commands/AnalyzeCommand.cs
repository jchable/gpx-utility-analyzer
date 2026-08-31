namespace GpxAiAnalyzer.Commands;

using GpxAiAnalyzer.Core.Analysis;
using GpxAiAnalyzer.Core.Models;
using GpxAiAnalyzer.Core.Output;
using GpxAiAnalyzer.Core.Providers;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

public static class AnalyzeCommand
{
    /// <param name="registry">Provider registry used to resolve <c>--provider</c>.</param>
    /// <param name="isInputRedirected">
    /// Stdin-availability probe. Defaults to <see cref="Console.IsInputRedirected"/>;
    /// overridable so the missing-input branch can be exercised from a test host,
    /// which always runs with a redirected stdin.
    /// </param>
    public static Command Create(ProviderRegistry registry, Func<bool>? isInputRedirected = null)
    {
        var inputRedirected = isInputRedirected ?? (() => Console.IsInputRedirected);

        var providerOption = new Option<string>("--provider")
        { Description = "AI provider: azure-openai, openai, anthropic, ollama, gemini", Required = true };

        var inputOption = new Option<FileInfo?>("--input")
        { Description = "JSON file with GPX stats (alternative to stdin pipe)" };

        var apiKeyOption = new Option<string?>("--api-key")
        { Description = "API key (overrides environment variable)" };

        var endpointOption = new Option<string?>("--endpoint")
        { Description = "Provider endpoint URL (overrides environment variable)" };

        var modelOption = new Option<string?>("--model")
        { Description = "Model name (provider-specific default if omitted)" };

        var formatOption = new Option<string>("--format")
        { Description = "Output format: text or json", DefaultValueFactory = _ => "text" };

        var command = new Command("analyze", "Analyze GPX track statistics using AI")
        {
            providerOption, inputOption, apiKeyOption, endpointOption, modelOption, formatOption
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var providerName = parseResult.GetValue(providerOption)!;
            var inputFile = parseResult.GetValue(inputOption);
            var apiKey = parseResult.GetValue(apiKeyOption);
            var endpoint = parseResult.GetValue(endpointOption);
            var model = parseResult.GetValue(modelOption);
            var format = parseResult.GetValue(formatOption)!;

            // Read JSON input
            string json;
            if (inputFile is not null)
            {
                json = await File.ReadAllTextAsync(inputFile.FullName, ct);
            }
            else if (inputRedirected())
            {
                json = await Console.In.ReadToEndAsync(ct);
            }
            else
            {
                Console.Error.WriteLine("Error: provide --input file or pipe JSON via stdin.");
                Console.Error.WriteLine("Usage: gpx-analyzer analyze --format json track.gpx | gpx-ai-analyzer analyze --provider openai");
                // A bare `return` here is exit code 0: a CI step redirecting stdout
                // to report.json wrote an empty file and its `if [ $? -ne 0 ]` guard
                // never fired.
                return 1;
            }

            // Deserialize
            var stats = JsonSerializer.Deserialize<GpxStats>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GPX stats JSON.");

            // Create provider client
            var options = new ProviderOptions
            {
                ApiKey = apiKey,
                Endpoint = endpoint,
                Model = model,
            };
            var chatClient = registry.CreateClient(providerName, options);

            // Run analysis
            var analyzer = new TrackAnalyzer(chatClient);
            var report = await analyzer.AnalyzeAsync(stats, ct: ct);

            // Output
            ReportFormatter.Format(Console.Out, stats.Filename, report, format);

            return 0;
        });

        return command;
    }
}
