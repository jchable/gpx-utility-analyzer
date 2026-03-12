using GpxAnalyzer.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that replaces the DbContext with an isolated
/// SQLite DB file and disables external services (AI, DEM downloads).
/// Each instance gets its own unique DB file — create one per test to guarantee isolation.
/// Note: ConfigureAppConfiguration doesn't work here because AddDbContext reads
/// the connection string at service registration time (before the config override runs).
/// Using ConfigureServices to replace the DbContextOptions is the correct pattern.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"gpxtest_{Guid.NewGuid():N}.db");

    private readonly string _storageDir =
        Path.Combine(Path.GetTempPath(), $"gpxtest_storage_{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // ── Replace DbContext with isolated test database ──────────────
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));

            // ── Ensure storage directories exist ──────────────────────────
            Directory.CreateDirectory(Path.Combine(_storageDir, "gpx"));
            Directory.CreateDirectory(Path.Combine(_storageDir, "dem"));
        });

        // Override file-based configuration values (AI, storage paths)
        // These are read lazily (not at DI registration time), so env override works.
        builder.UseEnvironment("Test");
        builder.UseSetting("AiProvider:Name", "");         // disable external AI calls
        builder.UseSetting("Storage:GpxDirectory", Path.Combine(_storageDir, "gpx"));
        builder.UseSetting("Storage:DemDirectory", Path.Combine(_storageDir, "dem"));
        builder.UseSetting("Routing:Provider", "");        // disable external routing calls
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        try { if (Directory.Exists(_storageDir)) Directory.Delete(_storageDir, recursive: true); } catch { /* best-effort */ }
    }
}
