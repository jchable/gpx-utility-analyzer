namespace GpxAnalyzer.Api.Controllers;

using System.Threading.Channels;
using GpxAnalyzer.Api.Data;
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
    private readonly Channel<(Guid ActivityId, Guid UserId)> _processingChannel;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        AppDbContext db,
        GpxStorageService storage,
        IEnumerable<IActivityImporter> importers,
        Channel<(Guid ActivityId, Guid UserId)> processingChannel,
        ILogger<WebhooksController> logger)
    {
        _db = db;
        _storage = storage;
        _importers = importers;
        _processingChannel = processingChannel;
        _logger = logger;
    }

    // Strava webhook validation (GET)
    [HttpGet("strava")]
    public async Task<IActionResult> StravaValidation()
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == "strava");
        if (importer is null) return NotFound();

        if (await importer.ValidateWebhookAsync(HttpContext))
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

        var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.Provider == provider && i.IsActive);
        if (integration is null)
        {
            _logger.LogWarning("Received webhook for {Provider} but no active integration found", provider);
            return Ok(); // Acknowledge but don't process
        }

        var externalId = await importer.GetWebhookActivityIdAsync(HttpContext);
        if (externalId is null) return Ok(); // Not an activity creation event

        // Check for duplicate
        var exists = await _db.Activities.AnyAsync(a => a.Source == provider && a.ExternalId == externalId);
        if (exists)
        {
            _logger.LogInformation("Activity {ExternalId} from {Provider} already exists, skipping", externalId, provider);
            return Ok();
        }

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
            };

            _db.Activities.Add(activity);
            await _db.SaveChangesAsync();

            await _processingChannel.Writer.WriteAsync((activity.Id, integration.UserId));

            _logger.LogInformation("Imported activity {ExternalId} from {Provider} as {Id} for user {UserId}", externalId, provider, activity.Id, integration.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import activity {ExternalId} from {Provider}", externalId, provider);
        }

        return Ok();
    }
}
