namespace GpxAiAnalyzer.Core.Providers;

using Anthropic.SDK;
using Microsoft.Extensions.AI;

public sealed class AnthropicProvider : IChatClientProvider
{
    public string Name => "anthropic";

    public IChatClient CreateClient(ProviderOptions options)
    {
        AnthropicClient client;

        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (apiKey is not null)
        {
            client = new AnthropicClient(apiKey);
        }
        else
        {
            // AnthropicClient() reads ANTHROPIC_API_KEY from environment by default
            client = new AnthropicClient();
        }

        return client.Messages;
    }
}
