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
    /// A segment with no measured path across it - a time gap larger than GapThreshold, or a
    /// &lt;trkseg&gt; boundary in the source - gets zero distance and speed.
    ///
    /// Owns <see cref="TrackPoint.AfterRecordingGap"/> and assigns it outright on every point:
    /// ComputePipeline re-runs this pass once anomaly correction has rewritten the timestamps,
    /// and a flag that could only ever be set to true would keep a boundary the corrected
    /// timestamps no longer justify. It never touches <see cref="TrackPoint.StartsNewSegment"/>,
    /// which describes the source file rather than the data.
    /// </summary>
    public static void EnrichPoints(List<TrackPoint> points)
    {
        if (points.Count == 0)
            return;

        points[0].AfterRecordingGap = false;

        for (int i = 1; i < points.Count; i++)
        {
            var dt = points[i].Time - points[i - 1].Time;
            points[i].AfterRecordingGap = dt > ElevationSmoother.GapThreshold;

            // BreaksRecordedTime, not AfterRecordingGap alone. A <trkseg> boundary means the
            // GPX said reception was lost or the receiver was off, so there is no measured
            // path between these two fixes however few seconds separate them - the straight
            // line across the break is an artefact of joining two recordings, not distance
            // anyone covered.
            //
            // Distance used to be the one statistic that ignored that: elevation sections and
            // stop runs split on BreaksPath and recorded time skips on BreaksRecordedTime, but
            // this loop looked only at dt, so a pause under GapThreshold banked the hop into
            // total_distance_m. That is also what left gpxa:dist unable to agree with the
            // total it is written beside (issue #144).
            //
            // SpeedClamped is deliberately not consulted here: ClampSpeeds owns it and runs
            // immediately after, so between the two passes DistFromPrev ends up zero on
            // exactly the segments BreaksPath describes.
            if (points[i].BreaksRecordedTime)
            {
                points[i].CalcSpeed = 0;
                points[i].DistFromPrev = 0;
                continue;
            }

            double dist = DistanceCalculator.Haversine(
                points[i - 1].Lat, points[i - 1].Lon,
                points[i].Lat, points[i].Lon);
            points[i].DistFromPrev = dist;
            points[i].CalcSpeed = dt.TotalSeconds > 0 ? dist / dt.TotalSeconds : 0;
        }
    }

    /// <summary>
    /// Zeroes out CalcSpeed and DistFromPrev for points exceeding maxSpeed.
    /// Preserves trace geometry (unlike FilterOutliers which removes points).
    ///
    /// Owns <see cref="TrackPoint.SpeedClamped"/> and assigns it outright, for the same reason
    /// EnrichPoints assigns its own flag. Clamping marks the DISTANCE between two fixes as
    /// unusable; it does not claim the recorder was off, so the seconds between them stay part
    /// of recorded time.
    /// </summary>
    public static int ClampSpeeds(List<TrackPoint> points, double maxSpeed)
    {
        if (points.Count == 0)
            return 0;

        points[0].SpeedClamped = false;

        int clamped = 0;
        for (int i = 1; i < points.Count; i++)
        {
            bool over = maxSpeed > 0 && points[i].CalcSpeed > maxSpeed;
            points[i].SpeedClamped = over;
            if (!over)
                continue;

            points[i].CalcSpeed = 0;
            points[i].DistFromPrev = 0;
            clamped++;
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
