namespace GpxAiAnalyzer.Providers;

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ClientModel;

public sealed class AzureOpenAIProvider : IChatClientProvider
{
    public string Name => "azure-openai";

    public IChatClient CreateClient(ProviderOptions options)
    {
        var endpoint = options.Endpoint
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException(
                "Azure OpenAI endpoint required. Set AZURE_OPENAI_ENDPOINT or use --endpoint.");

        var model = options.Model ?? "gpt-4o-mini";

        AzureOpenAIClient aoaiClient;
        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        if (apiKey is not null)
        {
            aoaiClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        }
        else
        {
            aoaiClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        }

        return aoaiClient.GetChatClient(model).AsIChatClient();
    }
}
