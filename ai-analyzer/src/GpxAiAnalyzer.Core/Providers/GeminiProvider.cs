namespace GpxAiAnalyzer.Core.Providers;

using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

/// <summary>
/// Google Gemini provider using the OpenAI-compatible endpoint.
/// No additional NuGet package needed — reuses the OpenAI SDK.
/// </summary>
public sealed class GeminiProvider : IChatClientProvider
{
    private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/";

    public string Name => "gemini";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = !string.IsNullOrWhiteSpace(options.ApiKey) ? options.ApiKey
            : Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? throw new InvalidOperationException(
                "Gemini API key required. Set GEMINI_API_KEY, GOOGLE_API_KEY, or use --api-key.");

        var model = !string.IsNullOrWhiteSpace(options.Model) ? options.Model : "gemini-2.0-flash";
        var endpoint = !string.IsNullOrWhiteSpace(options.Endpoint) ? options.Endpoint : DefaultEndpoint;

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

        return client.GetChatClient(model).AsIChatClient();
    }
}
