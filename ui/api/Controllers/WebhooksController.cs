namespace GpxAnalyzer.Api.Controllers;

using System.Threading.Channels;
using System.Security.Cryptography;
using System.Text;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.BackgroundServices;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GpxStorageService _storage;
    private readonly IEnumerable<IActivityImporter> _importers;
    private readonly Channel<ProcessingRequest> _processingChannel;
    private readonly ILogger<WebhooksController> _logger;
    private readonly ISettingsService _settings;

    public WebhooksController(
        AppDbContext db,
        GpxStorageService storage,
        IEnumerable<IActivityImporter> importers,
        Channel<ProcessingRequest> processingChannel,
        ILogger<WebhooksController> logger,
        ISettingsService settings)
    {
        _db = db;
        _storage = storage;
        _importers = importers;
        _processingChannel = processingChannel;
        _logger = logger;
        _settings = settings;
    }

    // Strava webhook validation (GET)
    [HttpGet("strava")]
    public async Task<IActionResult> StravaValidation()
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == "strava");
        if (importer is null) return NotFound();

        if (await importer.ValidateSubscriptionAsync(HttpContext))
        {
            var challenge = Request.Query["hub.challenge"].ToString();
            return Ok(new { hub_challenge = challenge });
        }

        return Unauthorized();
    }

    // Garmin webhook validation (GET)
    [HttpGet("garmin")]
    public IActionResult GarminValidation()
    {
        // Garmin validates webhook URL via GET during registration
        return Ok();
    }

    // Generic webhook handler
    [HttpPost("{provider}")]
    public async Task<IActionResult> HandleWebhook(string provider)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound();

        var expectedSecret = await _settings.GetAsync($"Integrations:{provider}:WebhookSecret");
        var suppliedSecret = Request.Query["secret"].ToString();
        if (string.IsNullOrWhiteSpace(expectedSecret) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSecret),
                Encoding.UTF8.GetBytes(suppliedSecret)))
        {
            _logger.LogWarning("Rejected unauthenticated webhook for {Provider}", provider);
            return Unauthorized();
        }

        // Read + validate the body once, before any credential is selected.
        var evt = await importer.ReadWebhookEventAsync(HttpContext);
        if (evt is null) return Ok(); // not an activity-create event, or failed validation

        if (string.IsNullOrEmpty(evt.OwnerId))
        {
            _logger.LogWarning("Webhook for {Provider} carried no owner id; dropping", provider);
            return Ok();
        }

        var integration = await _db.Integrations.FirstOrDefaultAsync(
            i => i.Provider == provider && i.IsActive && i.ExternalUserId == evt.OwnerId);
        if (integration is null)
        {
            _logger.LogWarning(
                "Received {Provider} webhook for owner {OwnerId} with no matching active integration",
                provider, evt.OwnerId);
            return Ok(); // Acknowledge but don't process
        }

        var externalId = evt.ExternalActivityId;

        // Check for duplicate — scoped to the owning user, matching
        // IX_Activities_UserId_Source_ExternalId. Two athletes who shared the same
        // workout each receive their own event and each get their own row.
        var exists = await _db.Activities.AnyAsync(a =>
            a.UserId == integration.UserId && a.Source == provider && a.ExternalId == externalId);
        if (exists)
        {
            _logger.LogInformation(
                "Activity {ExternalId} from {Provider} already exists for user {UserId}, skipping",
                externalId, provider, integration.UserId);
            return Ok();
        }

        string? uncommittedPath = null;
        var activitySaved = false;
        try
        {
            // Refresh token if needed
            if (integration.TokenExpiresAt.HasValue && integration.TokenExpiresAt.Value < DateTime.UtcNow)
            {
                if (integration.RefreshToken is not null)
                {
                    var newToken = await importer.RefreshTokenAsync(integration.RefreshToken);
                    integration.AccessToken = newToken.AccessToken;
                    integration.RefreshToken = newToken.RefreshToken;
                    integration.TokenExpiresAt = newToken.ExpiresAt;
                    integration.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }

            var imported = await importer.FetchActivityAsync(externalId, integration.AccessToken);
            var relativePath = await _storage.StoreAsync(imported.GpxStream, $"{provider}_{externalId}.gpx");
            uncommittedPath = relativePath;
            var leaseId = Guid.NewGuid();

            var activity = new Activity
            {
                Id = Guid.NewGuid(),
                UserId = integration.UserId,
                Name = imported.Name,
                ActivityType = imported.ActivityType,
                GpxFilePath = relativePath,
                Source = provider,
                ExternalId = externalId,
                Status = ProcessingStatus.Pending,
                ProcessingLeaseId = leaseId,
                ProcessingLeaseExpiresAt = DateTime.UtcNow.AddMinutes(1),
            };

            _db.Activities.Add(activity);
            await _db.SaveChangesAsync();
            activitySaved = true;

            await _processingChannel.Writer.WriteAsync(
                new ProcessingRequest(activity.Id, integration.UserId, leaseId));

            _logger.LogInformation("Imported activity {ExternalId} from {Provider} as {Id} for user {UserId}", externalId, provider, activity.Id, integration.UserId);
        }
        catch (Exception ex)
        {
            if (!activitySaved && uncommittedPath is not null)
            {
                try
                {
                    await _storage.DeleteAsync(uncommittedPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx,
                        "Failed to clean up uncommitted webhook file {Path}", uncommittedPath);
                }
            }
            _logger.LogError(ex, "Failed to import activity {ExternalId} from {Provider}", externalId, provider);
        }

        return Ok();
    }
}
