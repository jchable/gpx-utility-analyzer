using System.IO.Compression;
using System.Threading.Channels;
using GpxAnalyzer.Api.BackgroundServices;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAiAnalyzer.Core.Providers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
var dbProvider = builder.Configuration["Database:Provider"] ?? "sqlite";
if (dbProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
{
    var connString = builder.Configuration["Database:ConnectionStrings:PostgreSql"]!;
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connString));
}
else
{
    var connString = builder.Configuration["Database:ConnectionStrings:Sqlite"] ?? "Data Source=data/gpxanalyzer.db";
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connString));
}

// AI Provider Registry
var registry = new ProviderRegistry();
registry.Register(new AzureOpenAIProvider());
registry.Register(new OpenAIProvider());
registry.Register(new AnthropicProvider());
registry.Register(new MistralProvider());
registry.Register(new OllamaProvider());
registry.Register(new GeminiProvider());
builder.Services.AddSingleton(registry);

// Settings
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISettingsService, SettingsService>();

// Response compression
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Services
builder.Services.AddSingleton<GpxStorageService>();
builder.Services.AddSingleton<GpxCliService>();
builder.Services.AddSingleton<AiAnalysisService>();
builder.Services.AddSingleton<ProfileComputationService>();
builder.Services.AddSingleton<ActivityProcessingService>();

// Processing channel
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>());
builder.Services.AddHostedService<ActivityProcessingWorker>();

// Integration services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IActivityImporter, StravaService>();
builder.Services.AddSingleton<IActivityImporter, GarminService>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Allow large GPX uploads (up to 100 MB)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 100 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

// CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5175", "http://localhost:5176")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseResponseCompression();
app.UseCors();

// Serve React static files in production
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
