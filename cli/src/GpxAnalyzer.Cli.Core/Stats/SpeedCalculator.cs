using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Speed and pace statistics.
/// </summary>
public sealed class SpeedResult
{
    public double AvgSpeed { get; set; }       // m/s over total time
    public double AvgMovingSpeed { get; set; } // m/s over moving time
    public double MaxSpeed { get; set; }       // m/s
    public TimeSpan AvgPace { get; set; }      // per km, over total time
    public TimeSpan AvgMovingPace { get; set; } // per km, over moving time
}

public static class SpeedCalculator
{
    /// <summary>Default max reasonable speed (25 m/s ~ 90 km/h).</summary>
    public const double DefaultMaxReasonableSpeed = 25.0;

    /// <summary>Per-preset GPS outlier removal thresholds (m/s).</summary>
    public static readonly Dictionary<string, double> PresetMaxSpeed = new()
    {
        [StopDetector.PresetHiking] = 4.0,
        [StopDetector.PresetTrail] = 7.0,
        [StopDetector.PresetCycling] = 25.0,
        [StopDetector.PresetRunning] = 7.0,
        [StopDetector.PresetSwimming] = 3.0,
        [StopDetector.PresetWalking] = 4.0,
    };

    /// <summary>
    /// Computes speed and pace statistics.
    /// </summary>
    public static SpeedResult ComputeSpeed(double totalDist, TimeSpan totalTime, TimeSpan movingTime)
    {
        var result = new SpeedResult();

        double totalSec = totalTime.TotalSeconds;
        double movingSec = movingTime.TotalSeconds;

        if (totalSec > 0)
        {
            result.AvgSpeed = totalDist / totalSec;
            double distKm = totalDist / 1000;
            if (distKm > 0)
                result.AvgPace = TimeSpan.FromSeconds(Math.Truncate(totalSec / distKm));
        }

        if (movingSec > 0)
        {
            result.AvgMovingSpeed = totalDist / movingSec;
            double distKm = totalDist / 1000;
            if (distKm > 0)
                result.AvgMovingPace = TimeSpan.FromSeconds(Math.Truncate(movingSec / distKm));
        }

        return result;
    }

    /// <summary>
    /// Computes distance from previous point and calculated speed for each point.
    /// Points separated by a time gap larger than GapThreshold get zero distance and speed.
    /// </summary>
    public static void EnrichPoints(List<TrackPoint> points)
    {
        for (int i = 1; i < points.Count; i++)
        {
            var dt = points[i].Time - points[i - 1].Time;
            if (dt > ElevationSmoother.GapThreshold)
            {
                points[i].CalcSpeed = 0;
                points[i].DistFromPrev = 0;
                continue;
            }

            double dist = DistanceCalculator.Haversine(
                points[i - 1].Lat, points[i - 1].Lon,
                points[i].Lat, points[i].Lon);
            points[i].DistFromPrev = dist;
            if (dt.TotalSeconds > 0)
                points[i].CalcSpeed = dist / dt.TotalSeconds;
        }
    }

    /// <summary>
    /// Zeroes out CalcSpeed and DistFromPrev for points exceeding maxSpeed.
    /// Preserves trace geometry (unlike FilterOutliers which removes points).
    /// </summary>
    public static int ClampSpeeds(List<TrackPoint> points, double maxSpeed)
    {
        if (maxSpeed <= 0)
            return 0;

        int clamped = 0;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].CalcSpeed > maxSpeed)
            {
                points[i].CalcSpeed = 0;
                points[i].DistFromPrev = 0;
                clamped++;
            }
        }
        return clamped;
    }

    /// <summary>
    /// Returns the maximum calculated speed from enriched points.
    /// </summary>
    public static double MaxSpeedFromPoints(List<TrackPoint> points)
    {
        double max = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].CalcSpeed > max)
                max = points[i].CalcSpeed;
        }
        return max;
    }
}
