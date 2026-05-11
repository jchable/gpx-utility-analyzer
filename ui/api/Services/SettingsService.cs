namespace GpxAnalyzer.Api.Services;

using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using Microsoft.EntityFrameworkCore;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public SettingsService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<string?> GetAsync(string key, string? fallback = null)
    {
        var setting = await _db.GlobalSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        return setting ?? _configuration[key] ?? fallback;
    }

    public async Task<string?> GetAsync(Guid userId, string key, string? fallback = null)
    {
        var userSetting = await _db.Settings
            .Where(s => s.UserId == userId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (userSetting is not null) return userSetting;

        // Fall back to global settings
        var globalSetting = await _db.GlobalSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        return globalSetting ?? _configuration[key] ?? fallback;
    }

    public async Task SetManyAsync(Guid userId, Dictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
        {
            var existing = await _db.Settings
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);

            if (existing is not null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.Settings.Add(new Setting { UserId = userId, Key = key, Value = value });
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetGlobalManyAsync(Dictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
        {
            var existing = await _db.GlobalSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (existing is not null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.GlobalSettings.Add(new GlobalSetting { Key = key, Value = value });
            }
        }

        await _db.SaveChangesAsync();
    }
}
