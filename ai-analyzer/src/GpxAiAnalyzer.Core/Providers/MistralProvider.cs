namespace GpxAiAnalyzer.Core.Providers;

using Microsoft.Extensions.AI;
using Mistral.SDK;

public sealed class MistralProvider : IChatClientProvider
{
    public const string DefaultModel = "mistral-large-latest";

    public string Name => "mistral";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = options.ApiKey
            ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
            ?? throw new InvalidOperationException(
                "Mistral API key required. Set MISTRAL_API_KEY or use --api-key.");

        var client = new MistralClient(apiKey);
        var model = options.Model ?? DefaultModel;

        // Same defect as AnthropicProvider: client.Completions carries no model and
        // TrackAnalyzer never sets ChatOptions.ModelId, so options.Model was lost.
        return new ModelBindingChatClient(client.Completions, model);
    }
}
