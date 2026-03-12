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
        // Global: look up setting where userId is null, then IConfiguration
        var setting = await _db.Settings
            .Where(s => s.UserId == null && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        return setting ?? _configuration[key] ?? fallback;
    }

    public async Task<string?> GetAsync(Guid userId, string key, string? fallback = null)
    {
        // Per-user setting
        var userSetting = await _db.Settings
            .Where(s => s.UserId == userId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (userSetting is not null) return userSetting;

        // Fall back to global setting (userId = null)
        var globalSetting = await _db.Settings
            .Where(s => s.UserId == null && s.Key == key)
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
                _db.Settings.Add(new Setting
                {
                    UserId = userId,
                    Key = key,
                    Value = value,
                });
            }
        }

        await _db.SaveChangesAsync();
    }
}
