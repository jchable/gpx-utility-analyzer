namespace GpxAnalyzer.Api.Services;

using System.Text.Json;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;

public class ActivityProcessingService
{
    private readonly GpxStorageService _storage;
    private readonly GpxCliService _cliService;
    private readonly AiAnalysisService _aiService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityProcessingService> _logger;

    public ActivityProcessingService(
        GpxStorageService storage,
        GpxCliService cliService,
        AiAnalysisService aiService,
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityProcessingService> logger)
    {
        _storage = storage;
        _cliService = cliService;
        _aiService = aiService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessActivityAsync(Guid activityId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activity = await db.Activities.FindAsync([activityId], ct);
        if (activity is null) return;

        try
        {
            // Step 1: Run Go CLI analysis
            activity.Status = ProcessingStatus.Analyzing;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var gpxFullPath = _storage.GetFullPath(activity.GpxFilePath);
            var stats = await _cliService.AnalyzeAsync(gpxFullPath, ct);

            // Store stats and populate summary fields
            activity.StatsJson = JsonSerializer.Serialize(stats);
            activity.DistanceKm = stats.TotalDistanceKm;
            activity.ElevationGainM = stats.ElevationGainM;
            activity.ElevationLossM = stats.ElevationLossM;
            activity.MovingTimeSeconds = stats.MovingTime.Seconds;

            if (DateTime.TryParse(stats.StartTime, out var start))
                activity.StartTime = start;
            if (DateTime.TryParse(stats.EndTime, out var end))
                activity.EndTime = end;

            // Step 2: Run AI analysis
            activity.Status = ProcessingStatus.AiProcessing;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            try
            {
                var report = await _aiService.AnalyzeAsync(stats, ct);
                activity.AiReportJson = JsonSerializer.Serialize(report);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI analysis failed for activity {Id}, continuing without AI report", activityId);
                // AI failure is non-fatal — we still have the stats
            }

            activity.Status = ProcessingStatus.Completed;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Activity {Id} processed successfully", activityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process activity {Id}", activityId);
            activity.Status = ProcessingStatus.Failed;
            activity.ErrorMessage = ex.Message;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
