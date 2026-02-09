namespace GpxAnalyzer.Api.Services;

using System.Diagnostics;
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
        var totalSw = Stopwatch.StartNew();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activity = await db.Activities.FindAsync([activityId], ct);
        if (activity is null)
        {
            _logger.LogWarning("Activity {Id} not found, skipping processing", activityId);
            return;
        }

        _logger.LogInformation("Starting processing for activity {Id} ({Name}, type={Type}, source={Source})",
            activityId, activity.Name, activity.ActivityType, activity.Source);

        try
        {
            // Step 1: Run Go CLI analysis
            activity.Status = ProcessingStatus.Analyzing;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var gpxFullPath = _storage.GetFullPath(activity.GpxFilePath);
            _logger.LogInformation("[{Id}] Step 1/2: Running Go CLI analysis on {Path}", activityId, activity.GpxFilePath);

            var stepSw = Stopwatch.StartNew();
            var stats = await _cliService.AnalyzeAsync(gpxFullPath, ct);
            stepSw.Stop();

            _logger.LogInformation("[{Id}] Go CLI completed in {Elapsed:F1}s — {Distance:F1} km, D+{Gain:F0}m, D-{Loss:F0}m, moving {MovingTime}",
                activityId, stepSw.Elapsed.TotalSeconds,
                stats.TotalDistanceKm, stats.ElevationGainM, stats.ElevationLossM,
                TimeSpan.FromSeconds(stats.MovingTime.Seconds).ToString(@"hh\:mm\:ss"));

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

            _logger.LogInformation("[{Id}] Step 2/2: Running AI analysis", activityId);

            try
            {
                stepSw.Restart();
                var report = await _aiService.AnalyzeAsync(stats, ct);
                stepSw.Stop();
                activity.AiReportJson = JsonSerializer.Serialize(report);
                _logger.LogInformation("[{Id}] AI analysis completed in {Elapsed:F1}s", activityId, stepSw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Id}] AI analysis failed, continuing without AI report: {Message}", activityId, ex.Message);
            }

            activity.Status = ProcessingStatus.Completed;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            totalSw.Stop();
            _logger.LogInformation("[{Id}] Processing completed in {Elapsed:F1}s (status=Completed)", activityId, totalSw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            _logger.LogError(ex, "[{Id}] Processing failed after {Elapsed:F1}s: {Message}", activityId, totalSw.Elapsed.TotalSeconds, ex.Message);
            activity.Status = ProcessingStatus.Failed;
            activity.ErrorMessage = ex.Message;
            activity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
