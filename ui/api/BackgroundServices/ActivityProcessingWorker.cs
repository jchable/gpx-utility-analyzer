namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Services;

public class ActivityProcessingWorker : BackgroundService
{
    private readonly Channel<ProcessingRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProcessingCancellationRegistry _cancellations;
    private readonly ILogger<ActivityProcessingWorker> _logger;

    public ActivityProcessingWorker(
        Channel<ProcessingRequest> channel,
        IServiceScopeFactory scopeFactory,
        ProcessingCancellationRegistry cancellations,
        ILogger<ActivityProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _cancellations = cancellations;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Activity processing worker started");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            var (activityId, userId, leaseId) = request;

            // Per-activity token so a DELETE can stop this one run without
            // disturbing the worker or anything else in the queue.
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _cancellations.Register(activityId, runCts);
            try
            {
                _logger.LogInformation("Processing activity {Id} for user {UserId}", activityId, userId);
                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<ActivityProcessingService>();
                await processingService.ProcessActivityAsync(activityId, userId, leaseId, runCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing activity {Id}", activityId);
            }
            finally
            {
                _cancellations.Unregister(activityId);
            }
        }
    }
}
