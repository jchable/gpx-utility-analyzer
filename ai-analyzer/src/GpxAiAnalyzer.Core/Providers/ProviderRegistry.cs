namespace GpxAiAnalyzer.Core.Providers;

using Microsoft.Extensions.AI;

/// <summary>
/// Dynamic registry for AI provider resolution at runtime.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IChatClientProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IChatClientProvider provider)
    {
        _providers[provider.Name] = provider;
    }

    public IChatClient CreateClient(string providerName, ProviderOptions options)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
        {
            var available = string.Join(", ", _providers.Keys.Order());
            throw new InvalidOperationException(
                $"Unknown provider '{providerName}'. Available providers: {available}");
        }

        return provider.CreateClient(options);
    }

    public IEnumerable<string> AvailableProviders => _providers.Keys.Order();
}
