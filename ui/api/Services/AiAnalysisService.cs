namespace GpxAnalyzer.Api.Services;

using GpxAiAnalyzer.Core.Analysis;
using GpxAiAnalyzer.Core.Models;
using GpxAiAnalyzer.Core.Providers;

public class AiAnalysisService
{
    private readonly ProviderRegistry _registry;
    private readonly ISettingsService _settings;
    private readonly ILogger<AiAnalysisService> _logger;

    public AiAnalysisService(
        ProviderRegistry registry,
        ISettingsService settings,
        ILogger<AiAnalysisService> logger)
    {
        _registry = registry;
        _settings = settings;
        _logger = logger;
    }

    public async Task<TrackReport?> AnalyzeAsync(GpxStats stats, string language = "en", CancellationToken ct = default)
        => await AnalyzeAsync(null, stats, language, ct);

    public async Task<TrackReport?> AnalyzeAsync(Guid? userId, GpxStats stats, string language = "en", CancellationToken ct = default)
    {
        var providerName = userId.HasValue
            ? await _settings.GetAsync(userId.Value, "AiProvider:Name")
            : await _settings.GetAsync("AiProvider:Name");
        if (string.IsNullOrWhiteSpace(providerName))
        {
            _logger.LogInformation("AI analysis skipped — no provider configured.");
            return null;
        }

        var options = new ProviderOptions
        {
            ApiKey = userId.HasValue
                ? await _settings.GetAsync(userId.Value, "AiProvider:ApiKey")
                : await _settings.GetAsync("AiProvider:ApiKey"),
            Endpoint = userId.HasValue
                ? await _settings.GetAsync(userId.Value, "AiProvider:Endpoint")
                : await _settings.GetAsync("AiProvider:Endpoint"),
            Model = userId.HasValue
                ? await _settings.GetAsync(userId.Value, "AiProvider:Model")
                : await _settings.GetAsync("AiProvider:Model"),
        };

        _logger.LogInformation("Running AI analysis with provider={Provider}, model={Model}",
            providerName, options.Model ?? "(default)");

        var chatClient = _registry.CreateClient(providerName, options);
        var analyzer = new TrackAnalyzer(chatClient);

        return await analyzer.AnalyzeAsync(stats, language, ct);
    }
}
