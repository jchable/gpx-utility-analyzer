namespace GpxAnalyzer.Api.Services;

using System.Diagnostics;
using System.Text.Json;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;

public class ActivityProcessingService
{
    private readonly AppDbContext _db;
    private readonly GpxStorageService _storage;
    private readonly GpxAnalysisService _analysisService;
    private readonly AiAnalysisService _aiService;
    private readonly ProfileComputationService _profileService;
    private readonly ILogger<ActivityProcessingService> _logger;

    public ActivityProcessingService(
        AppDbContext db,
        GpxStorageService storage,
        GpxAnalysisService analysisService,
        AiAnalysisService aiService,
        ProfileComputationService profileService,
        ILogger<ActivityProcessingService> logger)
    {
        _db = db;
        _storage = storage;
        _analysisService = analysisService;
        _aiService = aiService;
        _profileService = profileService;
        _logger = logger;
    }

    public async Task ProcessActivityAsync(Guid activityId, Guid userId, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();

        var activity = await _db.Activities.FindAsync([activityId], ct);
        if (activity is null)
        {
            _logger.LogWarning("Activity {Id} not found, skipping processing", activityId);
            return;
        }

        _logger.LogInformation("Starting processing for activity {Id} ({Name}, type={Type}, source={Source})",
            activityId, activity.Name, activity.ActivityType, activity.Source);

        try
        {
            // Step 1: Run GPX analysis
            activity.Status = ProcessingStatus.Analyzing;
            activity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Determine which GPX to analyze: original archive (reanalyze) or uploaded file (first run)
            string gpxToAnalyze;
            string? tempExtractedPath = null;
            if (_storage.HasOriginalArchive(activity.GpxFilePath))
            {
                // Reanalyze: extract original from zip
                tempExtractedPath = _storage.ExtractOriginalToTemp(activity.GpxFilePath);
                gpxToAnalyze = tempExtractedPath;
                _logger.LogInformation("[{Id}] Reanalyze: extracted original from archive", activityId);
            }
            else
            {
                gpxToAnalyze = _storage.GetFullPath(activity.GpxFilePath);
            }

            _logger.LogInformation("[{Id}] Step 1/2: Running GPX analysis on {Path}", activityId, activity.GpxFilePath);

            // Create temp export directory for the processed GPX
            var exportDir = Path.Combine(Path.GetTempPath(), $"gpx-export-{Guid.NewGuid()}");
            Directory.CreateDirectory(exportDir);

            var stepSw = Stopwatch.StartNew();
            var stats = await _analysisService.AnalyzeAsync(userId, gpxToAnalyze, activity.ActivityType, exportDir, ct);
            stepSw.Stop();

            _logger.LogInformation("[{Id}] GPX analysis completed in {Elapsed:F1}s — {Distance:F1} km, D+{Gain:F0}m, D-{Loss:F0}m, moving {MovingTime}",
                activityId, stepSw.Elapsed.TotalSeconds,
                stats.TotalDistanceKm, stats.ElevationGainM, stats.ElevationLossM,
                TimeSpan.FromSeconds(stats.MovingTime.Seconds).ToString(@"hh\:mm\:ss"));

            // Archive original GPX as zip (only on first processing, skipped if already archived)
            if (tempExtractedPath is null)
            {
                _storage.ArchiveOriginalAsZip(activity.GpxFilePath);
                _logger.LogInformation("[{Id}] Archived original GPX as zip", activityId);
            }

            // Move processed GPX to replace the original path
            var exportedFiles = Directory.GetFiles(exportDir, "*_processed.gpx");
            if (exportedFiles.Length > 0)
            {
                _storage.ReplaceWithProcessed(activity.GpxFilePath, exportedFiles[0]);
                _logger.LogInformation("[{Id}] Replaced GPX with processed version", activityId);
            }

            // Compute profile and track GeoJSON from enriched GPX
            stepSw.Restart();
            var enrichedGpxPath = _storage.GetFullPath(activity.GpxFilePath);
            var (profileJson, trackGeoJson, splitsJson) = _profileService.ComputeFromEnrichedGpx(enrichedGpxPath);
            stepSw.Stop();

            activity.ProfileJson = profileJson;
            activity.TrackGeoJson = trackGeoJson;
            activity.SplitsJson = splitsJson;
            _logger.LogInformation("[{Id}] Profile computed in {Elapsed:F1}s ({ProfileSize} profile, {TrackSize} track)",
                activityId, stepSw.Elapsed.TotalSeconds,
                profileJson?.Length.ToString("N0") ?? "null",
                trackGeoJson?.Length.ToString("N0") ?? "null");

            // Cleanup temp directories
            try
            {
                if (Directory.Exists(exportDir))
                    Directory.Delete(exportDir, recursive: true);
                if (tempExtractedPath is not null)
                    Directory.Delete(Path.GetDirectoryName(tempExtractedPath)!, recursive: true);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "[{Id}] Temp cleanup failed (non-critical)", activityId);
            }

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
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[{Id}] Step 2/2: Running AI analysis", activityId);

            try
            {
                stepSw.Restart();
                var report = await _aiService.AnalyzeAsync(userId, stats, activity.Language, ct);
                stepSw.Stop();
                if (report is not null)
                {
                    activity.AiReportJson = JsonSerializer.Serialize(report);
                    _logger.LogInformation("[{Id}] AI analysis completed in {Elapsed:F1}s", activityId, stepSw.Elapsed.TotalSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Id}] AI analysis failed, continuing without AI report: {Message}", activityId, ex.Message);
            }

            activity.Status = ProcessingStatus.Completed;
            activity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

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
            await _db.SaveChangesAsync(ct);
        }
    }
}
