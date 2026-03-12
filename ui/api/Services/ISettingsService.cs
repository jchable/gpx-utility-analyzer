namespace GpxAnalyzer.Api.Services;

public interface ISettingsService
{
    /// <summary>Global setting lookup: global (userId=null) → IConfiguration → fallback.</summary>
    Task<string?> GetAsync(string key, string? fallback = null);

    /// <summary>Per-user setting lookup: user → global → IConfiguration → fallback.</summary>
    Task<string?> GetAsync(Guid userId, string key, string? fallback = null);

    Task SetManyAsync(Guid userId, Dictionary<string, string> settings);
}
