namespace GpxAiAnalyzer.Core.Providers;

using Microsoft.Extensions.AI;

/// <summary>
/// Factory interface for creating IChatClient instances for a specific AI provider.
/// Implement this to add a new provider.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>Unique name used to identify the provider.</summary>
    string Name { get; }

    /// <summary>Creates a configured IChatClient for this provider.</summary>
    IChatClient CreateClient(ProviderOptions options);
}

/// <summary>
/// Runtime configuration passed from CLI arguments, environment variables, or app settings.
/// </summary>
public sealed class ProviderOptions
{
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? Model { get; init; }
}
