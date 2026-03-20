namespace GpxAnalyzer.Api.Services;

using System.Diagnostics;
using System.Text.Json;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services.Storage;
using GpxAnalyzer.Cli.Core.Stats;
using Microsoft.EntityFrameworkCore;

public class ActivityProcessingService
{
    private readonly AppDbContext _db;
    private readonly GpxStorageService _storage;
    private readonly GpxAnalysisService _analysisService;
    private readonly AiAnalysisService _aiService;
    private readonly ProfileComputationService _profileService;
    private readonly ISettingsService _settings;
    private readonly ILogger<ActivityProcessingService> _logger;

    public ActivityProcessingService(
        AppDbContext db,
        GpxStorageService storage,
        GpxAnalysisService analysisService,
        AiAnalysisService aiService,
        ProfileComputationService profileService,
        ISettingsService settings,
        ILogger<ActivityProcessingService> logger)
    {
        _db = db;
        _storage = storage;
        _analysisService = analysisService;
        _aiService = aiService;
        _profileService = profileService;
        _settings = settings;
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

        GpxAiAnalyzer.Core.Models.GpxStats? analysisStats = null;

        try
        {
            // Step 1: Run GPX analysis
            activity.Status = ProcessingStatus.Analyzing;
            activity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Determine which GPX to analyze: original archive (reanalyze) or uploaded file (first run)
            var isReanalyze = await _storage.HasOriginalArchiveAsync(activity.GpxFilePath, ct);
            LocalFileLease? gpxLease = null;

            try
            {
                if (isReanalyze)
                {
                    gpxLease = await _storage.ExtractOriginalToTempAsync(activity.GpxFilePath, ct);
                    _logger.LogInformation("[{Id}] Reanalyze: extracted original from archive", activityId);
                }
                else
                {
                    gpxLease = await _storage.GetLocalPathAsync(activity.GpxFilePath, ct);
                }

                var gpxToAnalyze = gpxLease.Path;
                _logger.LogInformation("[{Id}] Step 1/2: Running GPX analysis on {Path}", activityId, activity.GpxFilePath);

                // Phase A: auto-detect activity type from GPX metadata (before analysis)
                var autoDetectStr = await _settings.GetAsync(userId, "GpxCli:AutoDetectActivityType");
                var autoDetect = bool.TryParse(autoDetectStr, out var ad) && ad;
                if (autoDetect)
                {
                    var gpxType = GpxAnalysisService.ExtractGpxType(gpxToAnalyze);
                    var detectedFromGpx = ActivityTypeDetector.DetectFromGpxType(gpxType);
                    if (detectedFromGpx != null)
                    {
                        _logger.LogInformation("[{Id}] Phase A: GPX type '{GpxType}' → {Detected}",
                            activityId, gpxType, detectedFromGpx);
                        activity.ActivityType = detectedFromGpx;
                    }
                }

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
                if (!isReanalyze)
                {
                    await _storage.ArchiveOriginalAsZipAsync(activity.GpxFilePath, ct);
                    _logger.LogInformation("[{Id}] Archived original GPX as zip", activityId);
                }

                // Move processed GPX to replace the original path
                var exportedFiles = Directory.GetFiles(exportDir, "*_processed.gpx");
                if (exportedFiles.Length > 0)
                {
                    await _storage.ReplaceWithProcessedAsync(activity.GpxFilePath, exportedFiles[0], ct);
                    _logger.LogInformation("[{Id}] Replaced GPX with processed version", activityId);
                }

                // Cleanup export dir
                try
                {
                    if (Directory.Exists(exportDir))
                        Directory.Delete(exportDir, recursive: true);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "[{Id}] Export dir cleanup failed (non-critical)", activityId);
                }

                // Compute profile and track GeoJSON from enriched GPX
                stepSw.Restart();
                using var enrichedLease = await _storage.GetLocalPathAsync(activity.GpxFilePath, ct);
                var (profileJson, trackGeoJson, splitsJson) = _profileService.ComputeFromEnrichedGpx(enrichedLease.Path);
                stepSw.Stop();

                activity.ProfileJson = profileJson;
                activity.TrackGeoJson = trackGeoJson;
                activity.SplitsJson = splitsJson;
                _logger.LogInformation("[{Id}] Profile computed in {Elapsed:F1}s ({ProfileSize} profile, {TrackSize} track)",
                    activityId, stepSw.Elapsed.TotalSeconds,
                    profileJson?.Length.ToString("N0") ?? "null",
                    trackGeoJson?.Length.ToString("N0") ?? "null");

                // Store stats and populate summary fields
                activity.StatsJson = JsonSerializer.Serialize(stats);
                analysisStats = stats;
                activity.DistanceKm = stats.TotalDistanceKm;
                activity.ElevationGainM = stats.ElevationGainM;
                activity.ElevationLossM = stats.ElevationLossM;
                activity.MovingTimeSeconds = stats.MovingTime.Seconds;

                if (DateTime.TryParse(stats.StartTime, out var start))
                    activity.StartTime = start;
                if (DateTime.TryParse(stats.EndTime, out var end))
                    activity.EndTime = end;

                // Phase B: auto-detect activity type from computed stats
                if (autoDetect)
                {
                    var detection = ActivityTypeDetector.DetectFromStats(stats);
                    _logger.LogInformation("[{Id}] Phase B: Detected {Type} ({Confidence:P0}), sub={SubType}",
                        activityId, detection.ActivityType, detection.Confidence, detection.SubType);
                    activity.ActivityType = detection.ActivityType;
                    activity.DetectedSubType = detection.SubType;
                }

                // Phase C: estimate calories using athlete profile
                var profile = await _db.AthleteProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
                var (kcal, method) = CalorieCalculator.Compute(
                    activity.ActivityType,
                    stats.MovingTime.Seconds,
                    stats.ElevationGainM,
                    stats.TotalDistanceKm,
                    stats.AvgMovingSpeedKmh,
                    stats.HeartRate?.AvgBpm,
                    profile?.WeightKg,
                    profile?.Sex,
                    profile?.Age);
                activity.EstimatedCalories = kcal > 0 ? kcal : null;
                activity.CalorieMethod = kcal > 0 ? method : null;
                _logger.LogInformation("[{Id}] Calories estimated: {Kcal:F0} kcal (method={Method})",
                    activityId, kcal, method);
            }
            finally
            {
                gpxLease?.Dispose();
            }

            // Step 2: Run AI analysis
            activity.Status = ProcessingStatus.AiProcessing;
            activity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[{Id}] Step 2/2: Running AI analysis", activityId);

            if (analysisStats is not null)
            {
                try
                {
                    var stepSw = Stopwatch.StartNew();
                    var report = await _aiService.AnalyzeAsync(userId, analysisStats, activity.Language, ct);
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
