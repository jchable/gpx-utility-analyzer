namespace GpxAiAnalyzer.Core.Providers;

using Microsoft.Extensions.AI;
using OpenAI;

public sealed class OpenAIProvider : IChatClientProvider
{
    public string Name => "openai";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = options.ApiKey
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OpenAI API key required. Set OPENAI_API_KEY or use --api-key.");

        var model = options.Model ?? "gpt-4o-mini";

        return new OpenAIClient(apiKey)
            .GetChatClient(model)
            .AsIChatClient();
    }
}
