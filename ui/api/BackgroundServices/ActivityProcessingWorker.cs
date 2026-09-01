namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Services;

public class ActivityProcessingWorker : BackgroundService
{
    private readonly Channel<ProcessingRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityProcessingWorker> _logger;

    public ActivityProcessingWorker(
        Channel<ProcessingRequest> channel,
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

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            var (activityId, userId, leaseId) = request;
            try
            {
                _logger.LogInformation("Processing activity {Id} for user {UserId}", activityId, userId);
                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<ActivityProcessingService>();
                await processingService.ProcessActivityAsync(activityId, userId, leaseId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing activity {Id}", activityId);
            }
        }
    }
}
