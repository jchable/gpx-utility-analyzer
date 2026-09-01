namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAiAnalyzer.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    private readonly ProviderRegistry _registry;

    public SettingsController(ISettingsService settings, ProviderRegistry registry)
    {
        _settings = settings;
        _registry = registry;
    }

    // ── User settings (analysis preferences) ────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<AppSettingsDto>> GetSettings()
    {
        var userId = User.GetUserId();
        var dto = new AppSettingsDto
        {
            Analysis = new AnalysisSettingsDto
            {
                Preset = await _settings.GetAsync(userId, "GpxCli:DefaultPreset", "trail") ?? "trail",
                Smoothing = await _settings.GetAsync(userId, "GpxCli:DefaultSmoothing", "medium") ?? "medium",
                TrackSmoothing = await _settings.GetAsync(userId, "GpxCli:DefaultTrackSmoothing", "medium") ?? "medium",
                ElevationAlgorithm = await _settings.GetAsync(userId, "GpxCli:ElevationAlgorithm", "threshold") ?? "threshold",
                FixAnomalies = bool.TryParse(await _settings.GetAsync(userId, "GpxCli:FixAnomalies"), out var fix) && fix,
                AutoDetectActivityType = bool.TryParse(await _settings.GetAsync(userId, "GpxCli:AutoDetectActivityType"), out var autoDetect) && autoDetect,
            },
        };

        return dto;
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] AppSettingsDto dto)
    {
        var userId = User.GetUserId();
        var updates = new Dictionary<string, string>
        {
            ["GpxCli:DefaultPreset"] = dto.Analysis.Preset,
            ["GpxCli:DefaultSmoothing"] = dto.Analysis.Smoothing,
            ["GpxCli:DefaultTrackSmoothing"] = dto.Analysis.TrackSmoothing,
            ["GpxCli:ElevationAlgorithm"] = dto.Analysis.ElevationAlgorithm,
            ["GpxCli:FixAnomalies"] = dto.Analysis.FixAnomalies.ToString().ToLowerInvariant(),
            ["GpxCli:AutoDetectActivityType"] = dto.Analysis.AutoDetectActivityType.ToString().ToLowerInvariant(),
        };

        await _settings.SetManyAsync(userId, updates);
        return NoContent();
    }

    // ── Global settings (admin only) ─────────────────────────────────────────

    [HttpGet("global")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GlobalSettingsDto>> GetGlobalSettings()
    {
        var dto = new GlobalSettingsDto
        {
            AiProvider = new AiProviderSettingsDto
            {
                Name = await _settings.GetAsync("AiProvider:Name") ?? "",
                HasApiKey = !string.IsNullOrEmpty(await _settings.GetAsync("AiProvider:ApiKey")),
                Model = await _settings.GetAsync("AiProvider:Model") ?? "",
                Endpoint = await _settings.GetAsync("AiProvider:Endpoint") ?? "",
                AvailableProviders = _registry.AvailableProviders.ToList(),
            },
            Integrations = new IntegrationCredentialsDto
            {
                Strava = new StravaCredentialsDto
                {
                    ClientId = await _settings.GetAsync("Integrations:Strava:ClientId") ?? "",
                    HasClientSecret = !string.IsNullOrEmpty(await _settings.GetAsync("Integrations:Strava:ClientSecret")),
                    HasWebhookSecret = !string.IsNullOrWhiteSpace(await _settings.GetAsync("Integrations:Strava:WebhookSecret")),
                },
                Garmin = new GarminCredentialsDto
                {
                    ConsumerKey = await _settings.GetAsync("Integrations:Garmin:ConsumerKey") ?? "",
                    HasConsumerSecret = !string.IsNullOrEmpty(await _settings.GetAsync("Integrations:Garmin:ConsumerSecret")),
                    HasWebhookSecret = !string.IsNullOrWhiteSpace(await _settings.GetAsync("Integrations:Garmin:WebhookSecret")),
                },
            },
        };

        return dto;
    }

    [HttpPut("global")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateGlobalSettings([FromBody] GlobalSettingsDto dto)
    {
        var updates = new Dictionary<string, string>();

        // AI Provider
        if (!string.IsNullOrEmpty(dto.AiProvider.Name))
            updates["AiProvider:Name"] = dto.AiProvider.Name;
        if (!string.IsNullOrEmpty(dto.AiProvider.ApiKey))
            updates["AiProvider:ApiKey"] = dto.AiProvider.ApiKey;
        if (!string.IsNullOrEmpty(dto.AiProvider.Model))
            updates["AiProvider:Model"] = dto.AiProvider.Model;
        updates["AiProvider:Endpoint"] = dto.AiProvider.Endpoint;

        // Strava credentials
        if (!string.IsNullOrEmpty(dto.Integrations.Strava.ClientId))
            updates["Integrations:Strava:ClientId"] = dto.Integrations.Strava.ClientId;
        if (!string.IsNullOrEmpty(dto.Integrations.Strava.ClientSecret))
            updates["Integrations:Strava:ClientSecret"] = dto.Integrations.Strava.ClientSecret;
        if (!string.IsNullOrEmpty(dto.Integrations.Strava.WebhookSecret))
            updates["Integrations:Strava:WebhookSecret"] = dto.Integrations.Strava.WebhookSecret;

        // Garmin credentials
        if (!string.IsNullOrEmpty(dto.Integrations.Garmin.ConsumerKey))
            updates["Integrations:Garmin:ConsumerKey"] = dto.Integrations.Garmin.ConsumerKey;
        if (!string.IsNullOrEmpty(dto.Integrations.Garmin.ConsumerSecret))
            updates["Integrations:Garmin:ConsumerSecret"] = dto.Integrations.Garmin.ConsumerSecret;
        if (!string.IsNullOrEmpty(dto.Integrations.Garmin.WebhookSecret))
            updates["Integrations:Garmin:WebhookSecret"] = dto.Integrations.Garmin.WebhookSecret;

        // Issue #143: startup refuses to boot a provider that has credentials but no
        // webhook secret. Credentials saved here are invisible to that check until the
        // next restart, so without this the save would silently 401 every webhook and
        // then block the restart that finally reported it.
        //
        // The check is against the state this update WOULD produce, not the request
        // body: only non-empty values are written above, so the client id and the
        // webhook secret may legitimately arrive in separate requests.
        var misconfiguration = await WebhookSecretValidator.FindMisconfigurationAsync(
            WebhookSecretValidator.ResolveAfterApplying(_settings, updates));

        // Refuse before persisting anything — a partial write would leave exactly the
        // broken state this exists to prevent. Rejecting rather than generating a
        // secret is deliberate: the operator needs its value to register the callback
        // URL, so a generated one hidden in the database would only move the silence.
        if (misconfiguration is not null)
            return BadRequest(new { code = "WEBHOOK_SECRET_REQUIRED", message = misconfiguration });

        await _settings.SetGlobalManyAsync(updates);
        return NoContent();
    }

    [HttpGet("providers")]
    public ActionResult<List<string>> GetProviders()
    {
        return _registry.AvailableProviders.ToList();
    }
}
