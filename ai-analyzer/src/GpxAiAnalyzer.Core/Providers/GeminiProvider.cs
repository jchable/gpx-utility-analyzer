namespace GpxAiAnalyzer.Core.Providers;

using Google.GenAI;
using Microsoft.Extensions.AI;

/// <summary>
/// Google Gemini provider using the official Google GenAI SDK.
/// </summary>
public sealed class GeminiProvider : IChatClientProvider
{
    public string Name => "gemini";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var apiKey = !string.IsNullOrWhiteSpace(options.ApiKey) ? options.ApiKey
            : Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? throw new InvalidOperationException(
                "Gemini API key required. Set GEMINI_API_KEY, GOOGLE_API_KEY, or use --api-key.");

        var model = !string.IsNullOrWhiteSpace(options.Model) ? options.Model : "gemini-2.0-flash";

        var client = new Client(apiKey: apiKey);
        return client.AsIChatClient(model);
    }
}
