namespace GpxAiAnalyzer.Core.Providers;

using Microsoft.Extensions.AI;

/// <summary>
/// Binds a default model onto every request an <see cref="IChatClient"/> makes.
/// <para>
/// The Anthropic and Mistral SDKs expose an <see cref="IChatClient"/> that takes no
/// model at construction (unlike <c>OpenAIClient.GetChatClient(model)</c>), and
/// <c>TrackAnalyzer</c> never sets <see cref="ChatOptions.ModelId"/> — so without
/// this wrapper the <c>--model</c> / <c>AiProvider:Model</c> value was dropped on
/// the floor. <c>ModelId ??=</c> leaves an explicit per-request model intact, so a
/// caller that does set one still wins.
/// </para>
/// <para>
/// It also reports the bound model as <see cref="ChatClientMetadata.DefaultModelId"/>,
/// which is what the OpenAI, Azure OpenAI, Ollama and Gemini clients already do.
/// </para>
/// </summary>
internal sealed class ModelBindingChatClient(IChatClient innerClient, string model)
    : DelegatingChatClient(innerClient)
{
    private readonly string _model = model;

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetResponseAsync(messages, Bind(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(messages, Bind(options), cancellationToken);

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is null && serviceType == typeof(ChatClientMetadata))
        {
            var inner = base.GetService(serviceType, serviceKey) as ChatClientMetadata;
            return new ChatClientMetadata(inner?.ProviderName, inner?.ProviderUri, _model);
        }

        return base.GetService(serviceType, serviceKey);
    }

    private ChatOptions Bind(ChatOptions? options)
    {
        var bound = options?.Clone() ?? new ChatOptions();
        bound.ModelId ??= _model;
        return bound;
    }
}
