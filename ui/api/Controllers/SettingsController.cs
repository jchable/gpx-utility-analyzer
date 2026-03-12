namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Services;
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
            AiProvider = new AiProviderSettingsDto
            {
                Name = await _settings.GetAsync(userId, "AiProvider:Name") ?? "",
                HasApiKey = !string.IsNullOrEmpty(await _settings.GetAsync(userId, "AiProvider:ApiKey")),
                Model = await _settings.GetAsync(userId, "AiProvider:Model") ?? "",
                Endpoint = await _settings.GetAsync(userId, "AiProvider:Endpoint") ?? "",
                AvailableProviders = _registry.AvailableProviders.ToList(),
            },
            Integrations = new IntegrationCredentialsDto
            {
                Strava = new StravaCredentialsDto
                {
                    ClientId = await _settings.GetAsync(userId, "Integrations:Strava:ClientId") ?? "",
                    HasClientSecret = !string.IsNullOrEmpty(await _settings.GetAsync(userId, "Integrations:Strava:ClientSecret")),
                },
                Garmin = new GarminCredentialsDto
                {
                    ConsumerKey = await _settings.GetAsync(userId, "Integrations:Garmin:ConsumerKey") ?? "",
                    HasConsumerSecret = !string.IsNullOrEmpty(await _settings.GetAsync(userId, "Integrations:Garmin:ConsumerSecret")),
                },
            },
        };

        return dto;
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] AppSettingsDto dto)
    {
        var userId = User.GetUserId();
        var updates = new Dictionary<string, string>();

        // Analysis settings
        updates["GpxCli:DefaultPreset"] = dto.Analysis.Preset;
        updates["GpxCli:DefaultSmoothing"] = dto.Analysis.Smoothing;
        updates["GpxCli:DefaultTrackSmoothing"] = dto.Analysis.TrackSmoothing;
        updates["GpxCli:ElevationAlgorithm"] = dto.Analysis.ElevationAlgorithm;
        updates["GpxCli:FixAnomalies"] = dto.Analysis.FixAnomalies.ToString().ToLowerInvariant();
        updates["GpxCli:AutoDetectActivityType"] = dto.Analysis.AutoDetectActivityType.ToString().ToLowerInvariant();

        // AI Provider
        if (!string.IsNullOrEmpty(dto.AiProvider.Name))
            updates["AiProvider:Name"] = dto.AiProvider.Name;
        if (!string.IsNullOrEmpty(dto.AiProvider.ApiKey))
            updates["AiProvider:ApiKey"] = dto.AiProvider.ApiKey;
        if (!string.IsNullOrEmpty(dto.AiProvider.Model))
            updates["AiProvider:Model"] = dto.AiProvider.Model;
        updates["AiProvider:Endpoint"] = dto.AiProvider.Endpoint;

        // Strava credentials (write secret only if non-empty)
        if (!string.IsNullOrEmpty(dto.Integrations.Strava.ClientId))
            updates["Integrations:Strava:ClientId"] = dto.Integrations.Strava.ClientId;
        if (!string.IsNullOrEmpty(dto.Integrations.Strava.ClientSecret))
            updates["Integrations:Strava:ClientSecret"] = dto.Integrations.Strava.ClientSecret;

        // Garmin credentials
        if (!string.IsNullOrEmpty(dto.Integrations.Garmin.ConsumerKey))
            updates["Integrations:Garmin:ConsumerKey"] = dto.Integrations.Garmin.ConsumerKey;
        if (!string.IsNullOrEmpty(dto.Integrations.Garmin.ConsumerSecret))
            updates["Integrations:Garmin:ConsumerSecret"] = dto.Integrations.Garmin.ConsumerSecret;

        await _settings.SetManyAsync(userId, updates);

        return NoContent();
    }

    [HttpGet("providers")]
    public ActionResult<List<string>> GetProviders()
    {
        return _registry.AvailableProviders.ToList();
    }
}
