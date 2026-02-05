namespace GpxAiAnalyzer.Providers;

using Microsoft.Extensions.AI;
using OllamaSharp;

public sealed class OllamaProvider : IChatClientProvider
{
    public string Name => "ollama";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var endpoint = options.Endpoint
            ?? Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
            ?? "http://localhost:11434";

        var model = options.Model ?? "llama3.1";

        return new OllamaApiClient(new Uri(endpoint), model);
    }
}
