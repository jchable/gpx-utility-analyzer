namespace GpxAiAnalyzer.Core.Providers;

using Anthropic.SDK;
using Microsoft.Extensions.AI;

public sealed class AnthropicProvider : IChatClientProvider
{
    public const string DefaultModel = "claude-sonnet-4-5";

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

        var model = options.Model ?? DefaultModel;

        // The SDK's IChatClient takes no model at construction, and TrackAnalyzer
        // does not set ChatOptions.ModelId — so without binding it here the
        // --model / AiProvider:Model value was dropped on the floor while the API
        // logged the model it thought it was using.
        return new ModelBindingChatClient(client.Messages, model);
    }
}
