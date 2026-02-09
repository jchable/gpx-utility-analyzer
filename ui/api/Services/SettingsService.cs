namespace GpxAnalyzer.Api.Services;

using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

public class SettingsService : ISettingsService
{
    private const string CacheKey = "settings:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public SettingsService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<string?> GetAsync(string key, string? fallback = null)
    {
        var all = await GetAllFromDbAsync();
        if (all.TryGetValue(key, out var value))
            return value;

        // Fall back to IConfiguration (appsettings.json)
        var configValue = _configuration[key];
        return configValue ?? fallback;
    }

    public async Task SetAsync(string key, string value)
    {
        await SetManyAsync(new Dictionary<string, string> { [key] = value });
    }

    public async Task SetManyAsync(Dictionary<string, string> settings)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var (key, value) in settings)
        {
            var existing = await db.Settings.FindAsync(key);
            if (existing is not null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.Settings.Add(new Setting
                {
                    Key = key,
                    Value = value,
                });
            }
        }

        await db.SaveChangesAsync();
        _cache.Remove(CacheKey);
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        return await GetAllFromDbAsync();
    }

    private async Task<Dictionary<string, string>> GetAllFromDbAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, string>? cached) && cached is not null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var settings = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        _cache.Set(CacheKey, settings, CacheDuration);
        return settings;
    }
}
