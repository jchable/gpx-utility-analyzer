namespace GpxAiAnalyzer.Providers;

using Microsoft.Extensions.AI;
using Mistral.SDK;

public sealed class MistralProvider : IChatClientProvider
{
    public string Name => "mistral";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = options.ApiKey
            ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
            ?? throw new InvalidOperationException(
                "Mistral API key required. Set MISTRAL_API_KEY or use --api-key.");

        var client = new MistralClient(apiKey);
        return client.Completions;
    }
}
