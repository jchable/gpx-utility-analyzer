using GpxAiAnalyzer.Core.Models;

namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Result of activity type detection.
/// </summary>
public record DetectionResult(string ActivityType, double Confidence, string? SubType = null);

/// <summary>
/// Detects activity type from GPX metadata and computed stats.
/// Phase A: from GPX &lt;trk&gt;&lt;type&gt; metadata.
/// Phase B: from computed GpxStats heuristics.
/// </summary>
public static class ActivityTypeDetector
{
    // Speed thresholds (km/h)
    private const double SwimMaxSpeed = 5.0;
    private const double CycleMinSpeed = 15.0;
    private const double CycleWithPowerMinSpeed = 12.0;
    private const double RunMinSpeed = 7.0;
    private const double RunMaxSpeed = 18.0;
    private const double TrailMinSpeed = 4.0;
    private const double TrailMaxSpeed = 15.0;
    private const double HikeMaxSpeed = 6.0;
    private const double WalkMaxSpeed = 6.0;

    // Elevation thresholds
    private const double SwimMaxElevGain = 20.0;
    private const double SwimMaxElevRange = 30.0;
    private const double RunMaxElevPerKm = 30.0;
    private const double TrailMinElevPerKm = 40.0;
    private const double TrailMinTerrainScore = 3.0;
    private const double HikeMinElevPerKm = 25.0;
    private const double WalkMaxElevPerKm = 20.0;

    // Backyard detection
    private const double BackyardLapDistanceKm = 6.706;
    // The guard compares this against the distance from estimatedLaps to its
    // nearest integer, which is at most 0.5 by definition — so 0.5 could never
    // reject anything. 0.15 is ~1 km of a 6.706 km lap.
    private const double BackyardLapTolerance = 0.15;
    private const int BackyardMinStops = 3;
    private const double BackyardMinIntervalMin = 50.0;
    private const double BackyardMaxIntervalMin = 70.0;
    private const double BackyardMaxCv = 0.15;
    private const double BackyardMinStopDurationSec = 180;
    private const double BackyardMaxStopDurationSec = 1800;

    /// <summary>
    /// Phase A: detect activity type from GPX &lt;trk&gt;&lt;type&gt; metadata.
    /// Returns null if the GPX type is not recognized.
    /// </summary>
    public static string? DetectFromGpxType(string? gpxType)
    {
        if (string.IsNullOrWhiteSpace(gpxType))
            return null;

        return gpxType.Trim().ToLowerInvariant() switch
        {
            "running" or "run" => "run",
            "trail_running" or "trail running" or "trail run" or "trailrun" => "trail",
            "hiking" or "hike" => "hike",
            "cycling" or "biking" or "ride" or "road_biking" or "mountain_biking"
                or "gravel_cycling" or "virtual_ride" => "cycle",
            "walking" or "walk" => "walk",
            "swimming" or "swim" or "lap_swimming" or "open_water_swimming" => "swim",
            _ => null
        };
    }

    /// <summary>
    /// Phase B: detect activity type from computed GpxStats using heuristics.
    /// </summary>
    public static DetectionResult DetectFromStats(GpxStats stats)
    {
        var avgSpeed = stats.AvgMovingSpeedKmh;
        var elevGain = stats.ElevationGainM;
        var distance = stats.TotalDistanceKm;
        var elevPerKm = distance > 0 ? elevGain / distance : 0;
        var elevRange = stats.MaxElevationM - stats.MinElevationM;
        var terrainScore = stats.Effort?.TerrainDifficulty.Score ?? 0;
        var hasPower = stats.Power != null;

        // 1. Swimming — very low speed, minimal elevation change
        if (avgSpeed < SwimMaxSpeed && elevGain < SwimMaxElevGain && elevRange < SwimMaxElevRange)
            return new DetectionResult("swim", 0.9);

        // 2. Cycling — high speed or moderate speed with power data
        if (avgSpeed > CycleMinSpeed)
            return new DetectionResult("cycle", 0.95);
        if (avgSpeed > CycleWithPowerMinSpeed && hasPower)
            return new DetectionResult("cycle", 0.85);

        // 3. Running (flat) — moderate-high speed, low elevation per km
        if (avgSpeed >= RunMinSpeed && avgSpeed <= RunMaxSpeed && elevPerKm < RunMaxElevPerKm)
        {
            var subType = DetectBackyard(stats);
            return new DetectionResult("run", 0.85, subType);
        }

        // 4. Trail running — moderate speed with significant elevation
        if (avgSpeed >= TrailMinSpeed && avgSpeed <= TrailMaxSpeed
            && (elevPerKm >= TrailMinElevPerKm || terrainScore >= TrailMinTerrainScore))
        {
            var subType = DetectBackyard(stats);
            return new DetectionResult("trail", 0.85, subType);
        }

        // 5. Ambiguous zone: speed 4-7 km/h — could be hike, walk, or slow trail
        // Resolve by elevation per km
        if (avgSpeed >= TrailMinSpeed && avgSpeed < RunMinSpeed)
        {
            if (elevPerKm >= HikeMinElevPerKm)
                return new DetectionResult("hike", 0.75);
            if (elevPerKm < WalkMaxElevPerKm)
                return new DetectionResult("walk", 0.70);
            return new DetectionResult("hike", 0.60); // borderline → default to hike
        }

        // 6. Slow activities (< 4 km/h)
        if (avgSpeed > 0 && avgSpeed < TrailMinSpeed)
        {
            if (elevPerKm >= HikeMinElevPerKm)
                return new DetectionResult("hike", 0.80);
            return new DetectionResult("walk", 0.75);
        }

        // Fallback
        return new DetectionResult("other", 0.3);
    }

    /// <summary>
    /// Detect backyard ultra sub-type from stop patterns.
    /// </summary>
    private static string? DetectBackyard(GpxStats stats)
    {
        var stops = stats.Stops;
        if (stops == null || stops.Count < BackyardMinStops)
            return null;

        // Filter stops to qualifying range (3-30 min)
        var qualifying = stops
            .Where(s => s.Duration.Seconds >= BackyardMinStopDurationSec
                     && s.Duration.Seconds <= BackyardMaxStopDurationSec)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (qualifying.Count < BackyardMinStops)
            return null;

        // Check regular intervals between stop start times
        var intervals = new List<double>();
        for (int i = 1; i < qualifying.Count; i++)
        {
            if (DateTime.TryParse(qualifying[i].StartTime, out var t1)
                && DateTime.TryParse(qualifying[i - 1].StartTime, out var t0))
            {
                intervals.Add((t1 - t0).TotalMinutes);
            }
        }

        if (intervals.Count == 0)
            return null;

        var avgInterval = intervals.Average();
        if (avgInterval < BackyardMinIntervalMin || avgInterval > BackyardMaxIntervalMin)
            return null;

        // Check coefficient of variation
        var stdDev = Math.Sqrt(intervals.Sum(x => (x - avgInterval) * (x - avgInterval)) / intervals.Count);
        var cv = stdDev / avgInterval;
        if (cv > BackyardMaxCv)
            return null;

        // Check distance matches N laps
        var estimatedLaps = stats.TotalDistanceKm / BackyardLapDistanceKm;
        if (Math.Abs(estimatedLaps - Math.Round(estimatedLaps)) > BackyardLapTolerance)
            return null;

        if (estimatedLaps < 3)
            return null;

        return "backyard";
    }
}
