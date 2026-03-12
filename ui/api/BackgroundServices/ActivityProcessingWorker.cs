namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Services;

public class ActivityProcessingWorker : BackgroundService
{
    private readonly Channel<(Guid ActivityId, Guid UserId)> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityProcessingWorker> _logger;

    public ActivityProcessingWorker(
        Channel<(Guid ActivityId, Guid UserId)> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Activity processing worker started");

        await foreach (var (activityId, userId) in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing activity {Id} for user {UserId}", activityId, userId);
                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<ActivityProcessingService>();
                await processingService.ProcessActivityAsync(activityId, userId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing activity {Id}", activityId);
            }
        }
    }
}
