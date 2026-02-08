namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Services;

public class ActivityProcessingWorker : BackgroundService
{
    private readonly Channel<Guid> _channel;
    private readonly ActivityProcessingService _processingService;
    private readonly ILogger<ActivityProcessingWorker> _logger;

    public ActivityProcessingWorker(
        Channel<Guid> channel,
        ActivityProcessingService processingService,
        ILogger<ActivityProcessingWorker> logger)
    {
        _channel = channel;
        _processingService = processingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Activity processing worker started");

        await foreach (var activityId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing activity {Id}", activityId);
                await _processingService.ProcessActivityAsync(activityId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing activity {Id}", activityId);
            }
        }
    }
}
