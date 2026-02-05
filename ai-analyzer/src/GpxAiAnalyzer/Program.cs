using GpxAiAnalyzer.Commands;
using GpxAiAnalyzer.Providers;
using System.CommandLine;

// Register all available providers
var registry = new ProviderRegistry();
registry.Register(new AzureOpenAIProvider());
registry.Register(new OpenAIProvider());
registry.Register(new AnthropicProvider());
registry.Register(new MistralProvider());
registry.Register(new OllamaProvider());

// Build CLI
var rootCommand = new RootCommand("AI-powered analysis of GPX track statistics")
{
    AnalyzeCommand.Create(registry)
};

return await rootCommand.Parse(args).InvokeAsync();
