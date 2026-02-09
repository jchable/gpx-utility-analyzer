namespace GpxAnalyzer.Api.Services;

public interface ISettingsService
{
    Task<string?> GetAsync(string key, string? fallback = null);
    Task SetAsync(string key, string value);
    Task SetManyAsync(Dictionary<string, string> settings);
    Task<Dictionary<string, string>> GetAllAsync();
}
