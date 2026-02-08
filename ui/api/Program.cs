using System.Threading.Channels;
using GpxAnalyzer.Api.BackgroundServices;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAiAnalyzer.Core.Providers;
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
builder.Services.AddSingleton(registry);

// Services
builder.Services.AddSingleton<GpxStorageService>();
builder.Services.AddSingleton<GpxCliService>();
builder.Services.AddSingleton<AiAnalysisService>();
builder.Services.AddSingleton<ActivityProcessingService>();

// Processing channel
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>());
builder.Services.AddHostedService<ActivityProcessingWorker>();

// Integration services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IActivityImporter, StravaService>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Auto-create database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

// Serve React static files in production
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
