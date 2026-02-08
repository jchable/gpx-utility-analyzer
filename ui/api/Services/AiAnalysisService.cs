namespace GpxAnalyzer.Api.Services;

using GpxAiAnalyzer.Core.Analysis;
using GpxAiAnalyzer.Core.Models;
using GpxAiAnalyzer.Core.Providers;

public class AiAnalysisService
{
    private readonly ProviderRegistry _registry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiAnalysisService> _logger;

    public AiAnalysisService(
        ProviderRegistry registry,
        IConfiguration configuration,
        ILogger<AiAnalysisService> logger)
    {
        _registry = registry;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TrackReport> AnalyzeAsync(GpxStats stats, CancellationToken ct = default)
    {
        var providerName = _configuration["AiProvider:Name"]
            ?? throw new InvalidOperationException("AI provider not configured. Set AiProvider:Name in settings.");

        var options = new ProviderOptions
        {
            ApiKey = _configuration["AiProvider:ApiKey"],
            Endpoint = _configuration["AiProvider:Endpoint"],
            Model = _configuration["AiProvider:Model"],
        };

        _logger.LogInformation("Running AI analysis with provider: {Provider}", providerName);

        var chatClient = _registry.CreateClient(providerName, options);
        var analyzer = new TrackAnalyzer(chatClient);

        return await analyzer.AnalyzeAsync(stats, ct);
    }
}
