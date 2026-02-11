namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Services;
using GpxAiAnalyzer.Core.Providers;
using Microsoft.AspNetCore.Mvc;

[ApiController]
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
        var dto = new AppSettingsDto
        {
            Athlete = new AthleteSettingsDto
            {
                MaxHeartRate = int.TryParse(await _settings.GetAsync("Athlete:MaxHR"), out var mhr) ? mhr : null,
                Age = int.TryParse(await _settings.GetAsync("Athlete:Age"), out var age) ? age : null,
                Ftp = int.TryParse(await _settings.GetAsync("Athlete:FTP"), out var ftp) ? ftp : null,
            },
            Analysis = new AnalysisSettingsDto
            {
                Preset = await _settings.GetAsync("GpxCli:DefaultPreset", "trail") ?? "trail",
                Smoothing = await _settings.GetAsync("GpxCli:DefaultSmoothing", "medium") ?? "medium",
                TrackSmoothing = await _settings.GetAsync("GpxCli:DefaultTrackSmoothing", "medium") ?? "medium",
                ElevationAlgorithm = await _settings.GetAsync("GpxCli:ElevationAlgorithm", "threshold") ?? "threshold",
            },
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
                },
                Garmin = new GarminCredentialsDto
                {
                    ConsumerKey = await _settings.GetAsync("Integrations:Garmin:ConsumerKey") ?? "",
                    HasConsumerSecret = !string.IsNullOrEmpty(await _settings.GetAsync("Integrations:Garmin:ConsumerSecret")),
                },
            },
        };

        return dto;
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] AppSettingsDto dto)
    {
        var updates = new Dictionary<string, string>();

        // Athlete settings
        if (dto.Athlete.MaxHeartRate.HasValue)
            updates["Athlete:MaxHR"] = dto.Athlete.MaxHeartRate.Value.ToString();
        if (dto.Athlete.Age.HasValue)
            updates["Athlete:Age"] = dto.Athlete.Age.Value.ToString();
        if (dto.Athlete.Ftp.HasValue)
            updates["Athlete:FTP"] = dto.Athlete.Ftp.Value.ToString();

        // Analysis settings
        updates["GpxCli:DefaultPreset"] = dto.Analysis.Preset;
        updates["GpxCli:DefaultSmoothing"] = dto.Analysis.Smoothing;
        updates["GpxCli:DefaultTrackSmoothing"] = dto.Analysis.TrackSmoothing;
        updates["GpxCli:ElevationAlgorithm"] = dto.Analysis.ElevationAlgorithm;

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

        await _settings.SetManyAsync(updates);

        return NoContent();
    }

    [HttpGet("providers")]
    public ActionResult<List<string>> GetProviders()
    {
        return _registry.AvailableProviders.ToList();
    }
}
