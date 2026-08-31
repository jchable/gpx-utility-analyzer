namespace GpxAiAnalyzer.Tests.Providers;

using GpxAiAnalyzer.Core.Providers;
using Microsoft.Extensions.AI;

public class ProviderRegistryTests
{
    [Fact]
    public void Register_AddsProvider()
    {
        var registry = new ProviderRegistry();
        registry.Register(new FakeProvider("test"));
        Assert.Contains("test", registry.AvailableProviders);
    }

    [Fact]
    public void CreateClient_UnknownProvider_ThrowsWithAvailableList()
    {
        var registry = new ProviderRegistry();
        registry.Register(new FakeProvider("openai"));
        registry.Register(new FakeProvider("anthropic"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.CreateClient("unknown", new ProviderOptions()));

        Assert.Contains("unknown", ex.Message);
        Assert.Contains("anthropic", ex.Message);
        Assert.Contains("openai", ex.Message);
    }

    [Fact]
    public void CreateClient_CaseInsensitive()
    {
        var registry = new ProviderRegistry();
        registry.Register(new FakeProvider("OpenAI"));

        // Should not throw
        var client = registry.CreateClient("openai", new ProviderOptions());
        Assert.NotNull(client);
    }

    [Fact]
    public void AvailableProviders_ReturnsSortedNames()
    {
        var registry = new ProviderRegistry();
        registry.Register(new FakeProvider("zeta"));
        registry.Register(new FakeProvider("alpha"));
        registry.Register(new FakeProvider("middle"));

        var providers = registry.AvailableProviders.ToList();
        Assert.Equal(["alpha", "middle", "zeta"], providers);
    }

    // #91: AnthropicProvider returned client.Messages and MistralProvider returned
    // client.Completions without ever reading options.Model, and TrackAnalyzer does
    // not set ChatOptions.ModelId either — so --model / AiProvider:Model was dropped
    // on the floor. The four working providers all report the requested model as
    // ChatClientMetadata.DefaultModelId; these two must do the same.
    [Theory]
    [InlineData("anthropic")]
    [InlineData("mistral")]
    public void CreateClient_HonoursTheRequestedModel(string providerName)
    {
        var registry = new ProviderRegistry();
        registry.Register(new AnthropicProvider());
        registry.Register(new MistralProvider());

        var client = registry.CreateClient(providerName, new ProviderOptions
        {
            ApiKey = "test-key-not-used-for-a-real-call",
            Model = "explicitly-requested-model",
        });

        var metadata = client.GetService(typeof(ChatClientMetadata)) as ChatClientMetadata;

        Assert.NotNull(metadata);
        Assert.Equal("explicitly-requested-model", metadata!.DefaultModelId);
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("mistral")]
    public void CreateClient_NoModelRequested_FallsBackToTheProviderDefault(string providerName)
    {
        var registry = new ProviderRegistry();
        registry.Register(new AnthropicProvider());
        registry.Register(new MistralProvider());

        var client = registry.CreateClient(providerName, new ProviderOptions
        {
            ApiKey = "test-key-not-used-for-a-real-call",
        });

        var metadata = client.GetService(typeof(ChatClientMetadata)) as ChatClientMetadata;

        Assert.NotNull(metadata);
        Assert.False(string.IsNullOrWhiteSpace(metadata!.DefaultModelId));
    }

    // The metadata assertions above prove the providers bind the requested model;
    // these pin what the binding does to the options of every outgoing request.
    [Fact]
    public async Task ModelBinding_NoPerRequestModel_InjectsTheBoundModel()
    {
        var inner = new CapturingChatClient();
        var client = new ModelBindingChatClient(inner, "bound-model");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.NotNull(inner.LastOptions);
        Assert.Equal("bound-model", inner.LastOptions!.ModelId);
    }

    [Fact]
    public async Task ModelBinding_ExplicitPerRequestModel_Wins()
    {
        var inner = new CapturingChatClient();
        var client = new ModelBindingChatClient(inner, "bound-model");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            new ChatOptions { ModelId = "caller-model" });

        Assert.Equal("caller-model", inner.LastOptions!.ModelId);
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public void Dispose() { }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            throw new NotImplementedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }

    private sealed class FakeProvider(string name) : IChatClientProvider
    {
        public string Name => name;

        public IChatClient CreateClient(ProviderOptions options)
        {
            return new FakeChatClient();
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public void Dispose() { }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "fake")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
