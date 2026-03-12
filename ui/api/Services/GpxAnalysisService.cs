namespace GpxAnalyzer.Api.Services;

using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Stats;
using GpxAiAnalyzer.Core.Models;

public class GpxAnalysisService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<GpxAnalysisService> _logger;

    public GpxAnalysisService(ISettingsService settings, ILogger<GpxAnalysisService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    private static readonly Dictionary<string, string> ActivityTypeToPreset = new()
    {
        ["run"] = StopDetector.PresetRunning,
        ["walk"] = StopDetector.PresetWalking,
        ["trail"] = StopDetector.PresetTrail,
        ["hike"] = StopDetector.PresetHiking,
        ["cycle"] = StopDetector.PresetCycling,
        ["swim"] = StopDetector.PresetSwimming,
    };

    public async Task<GpxStats> AnalyzeAsync(string gpxFilePath, string? activityType = null, string? exportDir = null, CancellationToken ct = default)
        => await AnalyzeAsync(null, gpxFilePath, activityType, exportDir, ct);

    public async Task<GpxStats> AnalyzeAsync(Guid? userId, string gpxFilePath, string? activityType = null, string? exportDir = null, CancellationToken ct = default)
    {
        var defaultPreset = userId.HasValue
            ? await _settings.GetAsync(userId.Value, "GpxCli:DefaultPreset", "trail") ?? "trail"
            : await _settings.GetAsync("GpxCli:DefaultPreset", "trail") ?? "trail";
        var preset = activityType != null && ActivityTypeToPreset.TryGetValue(activityType, out var mapped)
            ? mapped
            : defaultPreset;
        var smoothing = userId.HasValue
            ? await _settings.GetAsync(userId.Value, "GpxCli:DefaultSmoothing", "medium") ?? "medium"
            : await _settings.GetAsync("GpxCli:DefaultSmoothing", "medium") ?? "medium";
        var trackSmoothing = userId.HasValue
            ? await _settings.GetAsync(userId.Value, "GpxCli:DefaultTrackSmoothing", "medium") ?? "medium"
            : await _settings.GetAsync("GpxCli:DefaultTrackSmoothing", "medium") ?? "medium";
        var fixAnomaliesStr = userId.HasValue
            ? await _settings.GetAsync(userId.Value, "GpxCli:FixAnomalies")
            : await _settings.GetAsync("GpxCli:FixAnomalies");
        var fixAnomalies = bool.TryParse(fixAnomaliesStr, out var fa) && fa;

        _logger.LogInformation("Analyzing {File} (preset={Preset}, smoothing={Smoothing}, trackSmoothing={TrackSmoothing})",
            gpxFilePath, preset, smoothing, trackSmoothing);

        // 1. Parse GPX
        var doc = GpxParser.ParseFile(gpxFilePath);
        var points = doc.AllPoints();

        // 2. Build ComputeConfig
        var cfg = BuildConfig(preset, smoothing, trackSmoothing, fixAnomalies);

        // 3. Run pipeline
        var (summary, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        _logger.LogInformation("Analysis complete: {Distance:F1} km, D+{Gain:F0}m, {Points} points",
            summary.TotalDistance / 1000, summary.Elevation.Gain, processed.Count);

        // 4. Export enriched GPX if requested
        if (!string.IsNullOrEmpty(exportDir))
        {
            var baseName = Path.GetFileNameWithoutExtension(gpxFilePath);
            var outPath = Path.Combine(exportDir, baseName + "_processed.gpx");
            Directory.CreateDirectory(exportDir);
            GpxWriter.WriteEnriched(outPath, processed, baseName);
            _logger.LogDebug("Exported enriched GPX: {Path} ({Count} points)", outPath, processed.Count);
        }

        // 5. Map Summary → GpxStats
        return SummaryMapper.ToGpxStats(gpxFilePath, summary);
    }

    private static ComputeConfig BuildConfig(string preset, string smoothing, string trackSmoothing, bool fixAnomalies = false)
    {
        if (!StopDetector.Presets.TryGetValue(preset, out var stopCfg))
            stopCfg = StopDetector.Presets[StopDetector.PresetTrail];

        if (!ElevationSmoother.IsValidLevel(smoothing))
            smoothing = "medium";

        if (!TrackSmoother.IsValidLevel(trackSmoothing))
            trackSmoothing = "medium";

        double maxReasonable = SpeedCalculator.PresetMaxSpeed.TryGetValue(preset, out var pm) ? pm : 0;

        string cacheDir = DemSource.DefaultCacheDir();
        var demSource = DemSource.CreateAuto(cacheDir);

        return new ComputeConfig
        {
            ElevationThreshold = 2.0,
            StopConfig = stopCfg,
            SmoothingLevel = smoothing,
            DemSource = demSource,
            ElevationCfg = new ElevationConfig
            {
                Algo = ElevationAlgo.Threshold,
                Threshold = 2.0,
            },
            TrackSmoothing = trackSmoothing,
            MaxReasonableSpeed = maxReasonable,
            AnomalyConfig = AnomalyConfig.Default(),
            FixAnomalies = fixAnomalies,
        };
    }
}
