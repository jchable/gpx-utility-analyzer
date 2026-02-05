namespace GpxAiAnalyzer.Tests.Providers;

using GpxAiAnalyzer.Providers;
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
