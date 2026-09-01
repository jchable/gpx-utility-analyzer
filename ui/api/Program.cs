using System.IO.Compression;
using System.Text;
using System.Threading.Channels;
using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.BackgroundServices;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Email;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAnalyzer.Api.Services.Storage;
using GpxAiAnalyzer.Core.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

// ASP.NET Identity
builder.Services.AddIdentityCore<ApplicationUser>(opt =>
{
    opt.Password.RequireDigit = false;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequiredLength = 8;
    opt.User.RequireUniqueEmail = true;
    opt.SignIn.RequireConfirmedEmail = false;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

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
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Response compression
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Object storage (local filesystem or S3-compatible)
var storageType = builder.Configuration["Storage:Type"]?.ToLowerInvariant() ?? "local";
if (storageType == "s3")
    builder.Services.AddScoped<IStorageService, S3StorageService>();
else
    builder.Services.AddScoped<IStorageService, LocalStorageService>();

// Email service
var emailType = builder.Configuration["Email:Type"]?.ToLowerInvariant() ?? "noop";
if (emailType == "smtp")
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
else
    builder.Services.AddScoped<IEmailService, NoOpEmailService>();

// Services (scoped for multi-user alignment with DbContext)
builder.Services.AddScoped<GpxStorageService>();
builder.Services.AddScoped<GpxAnalysisService>();
builder.Services.AddScoped<AiAnalysisService>();
builder.Services.AddScoped<ProfileComputationService>();
builder.Services.AddScoped<ActivityProcessingService>();
builder.Services.AddScoped<RouteService>();
builder.Services.AddScoped<RouteElevationService>();
builder.Services.AddScoped<RacePlanService>();
builder.Services.AddScoped<NutritionProductService>();

// Routing service (ORS or OSRM based on config)
var routingProvider = builder.Configuration["Routing:Provider"]?.ToLowerInvariant();
if (routingProvider == "ors")
{
    builder.Services.AddScoped<GpxAnalyzer.Api.Services.Routing.IRoutingService,
        GpxAnalyzer.Api.Services.Routing.OrsRoutingService>();
}
else if (routingProvider == "osrm")
{
    builder.Services.AddScoped<GpxAnalyzer.Api.Services.Routing.IRoutingService,
        GpxAnalyzer.Api.Services.Routing.OsrmRoutingService>();
}

// Processing channel — carries (ActivityId, UserId) for multi-user context
builder.Services.AddSingleton(Channel.CreateUnbounded<ProcessingRequest>());
// Lets a DELETE stop the run it is deleting instead of paying for it to finish.
builder.Services.AddSingleton<ProcessingCancellationRegistry>();
// Registered BEFORE the worker so stranded rows are requeued before it starts reading.
builder.Services.AddHostedService<ProcessingRecoveryService>();
builder.Services.AddHostedService<ActivityProcessingWorker>();

// Integration services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IActivityImporter, StravaService>();
builder.Services.AddScoped<IActivityImporter, GarminService>();

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

// Auto-migrate database and seed roles
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>().CreateLogger("GpxAnalyzer.Api.Startup");

    await ExternalActivityDeduplication.LogRowsAboutToBeRemovedAsync(db, startupLogger);
    db.Database.Migrate();

    // Seed roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    string[] roles = ["Admin", "Premium", "User"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
    }
}

// Ensure S3 bucket exists when using S3 storage
if (storageType == "s3")
{
    using var scope = app.Services.CreateScope();
    var s3 = (S3StorageService)scope.ServiceProvider.GetRequiredService<IStorageService>();
    await s3.EnsureBucketAsync();
}

app.UseResponseCompression();
app.UseCors();

// Serve React static files in production
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
