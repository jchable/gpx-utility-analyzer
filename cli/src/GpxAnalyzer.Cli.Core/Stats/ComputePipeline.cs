using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

public static class ComputePipeline
{
    /// <summary>
    /// Calculates all statistics from the given trackpoints.
    /// Returns the summary and the processed (filtered + corrected) point list.
    /// </summary>
    public static (Summary Summary, List<TrackPoint> Points) Compute(
        List<TrackPoint> points, int segmentCount, ComputeConfig cfg)
    {
        var s = new Summary
        {
            PointCount = points.Count,
            SegmentCount = segmentCount
        };

        if (points.Count == 0)
            return (s, points);

        // Step 0: Remove GPS outliers
        (points, s.FilteredPoints) = GpsFilter.FilterOutliers(points, cfg.MaxReasonableSpeed);

        // Step 1: Track smoothing (lat/lon)
        TrackSmoother.SmoothTrack(points, cfg.TrackSmoothing);

        // Step 2: DEM preload + correction
        if (cfg.DemSource != null)
        {
            if (cfg.DemSource is IElevationPreloader preloader)
                preloader.PreloadAsync(points).GetAwaiter().GetResult();

            for (int i = 0; i < points.Count; i++)
            {
                var (ele, ok) = cfg.DemSource.GetElevation(points[i].Lat, points[i].Lon);
                if (ok) points[i].Ele = ele;
            }
        }

        // Step 3: Elevation smoothing
        ElevationSmoother.SmoothElevations(points, cfg.SmoothingLevel);

        // Step 4: Enrich points with distance and speed
        SpeedCalculator.EnrichPoints(points);

        // Step 5: Clamp remaining speed artifacts
        SpeedCalculator.ClampSpeeds(points, cfg.MaxReasonableSpeed);

        // Step 6-7: Distance
        for (int i = 1; i < points.Count; i++)
        {
            s.TotalDistance += points[i].DistFromPrev;
            s.TotalDistance3D += DistanceCalculator.Distance3D(
                points[i - 1].Lat, points[i - 1].Lon, points[i - 1].Ele,
                points[i].Lat, points[i].Lon, points[i].Ele);
        }

        // Step 8: Elevation (configured algorithm)
        var elevCfg = new ElevationConfig
        {
            Algo = string.IsNullOrEmpty(cfg.ElevationCfg.Algo) ? ElevationAlgo.Threshold : cfg.ElevationCfg.Algo,
            Threshold = cfg.ElevationCfg.Threshold == 0 ? cfg.ElevationThreshold : cfg.ElevationCfg.Threshold,
            Epsilon = cfg.ElevationCfg.Epsilon,
            MinSegLen = cfg.ElevationCfg.MinSegLen,
            MaxSlopeDev = cfg.ElevationCfg.MaxSlopeDev,
        };
        s.Elevation = ElevationCalculator.ComputeWithAlgo(points, elevCfg);

        // Step 9: Time
        s.StartTime = points[0].Time;
        s.EndTime = points[^1].Time;
        s.TotalTime = s.EndTime - s.StartTime;

        // Step 10-11: Stops
        s.Stops = StopDetector.DetectStops(points, cfg.StopConfig);
        s.StopCount = s.Stops.Count;
        s.TotalStopTime = StopDetector.TotalStopTime(s.Stops);
        s.LongestStop = StopDetector.LongestStop(s.Stops);
        s.AvgStopDuration = StopDetector.AvgStopDuration(s.Stops);
        s.StoppedTime = s.TotalStopTime;
        s.MovingTime = s.TotalTime - s.StoppedTime;
        if (s.MovingTime < TimeSpan.Zero)
            s.MovingTime = TimeSpan.Zero;

        // Step 12: Speed & Pace
        s.Speed = SpeedCalculator.ComputeSpeed(s.TotalDistance, s.TotalTime, s.MovingTime);
        s.Speed.MaxSpeed = SpeedCalculator.MaxSpeedFromPoints(points);

        // Step 13: Points per km
        if (s.TotalDistance > 0)
            s.PointsPerKm = points.Count / (s.TotalDistance / 1000);

        // Step 14: Biometrics
        s.Biometrics = BiometricsCalculator.Compute(points, cfg.BiometricsCfg);

        // Step 15: Effort metrics
        s.Effort = EffortCalculator.ComputeAll(points, s);

        return (s, points);
    }
}
