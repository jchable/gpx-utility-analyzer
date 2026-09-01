using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

/// <summary>
/// Who owns each recording-boundary bit on a TrackPoint.
///
/// <see cref="TrackPoint.StartsNewSegment"/> describes the SOURCE GPX: the file opened a new
/// &lt;trkseg&gt; here. Only the GPX layer writes it.
///
/// <see cref="TrackPoint.AfterRecordingGap"/> and <see cref="TrackPoint.SpeedClamped"/> are
/// DERIVED from the current point data, and <see cref="ComputePipeline"/> re-derives them after
/// anomaly correction has rewritten timestamps and positions. A derived bit that a pass can
/// only ever set - never clear - therefore accumulates across passes and outlives the data that
/// justified it. These tests pin that each pass fully recomputes what it owns, and leaves what
/// it does not own alone.
/// </summary>
public class SegmentBoundaryOwnershipTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static List<TrackPoint> ThreePoints(params double[] minutes) =>
        [.. minutes.Select((m, i) => new TrackPoint
        {
            Lat = 45.0 + i * 0.001,
            Lon = 6.0,
            Ele = 100 + i * 10,
            Time = T0.AddMinutes(m),
        })];

    // ------------------------------------------------------- EnrichPoints owns the gap bit

    [Fact]
    public void EnrichPoints_RecordingGap_MarksTheBoundaryAndVoidsTheSegment()
    {
        var points = ThreePoints(0, 1, 42);   // 41 min > the 10 min gap threshold

        SpeedCalculator.EnrichPoints(points);

        Assert.True(points[2].AfterRecordingGap);
        Assert.Equal(0, points[2].DistFromPrev);
        Assert.Equal(0, points[2].CalcSpeed);
        Assert.False(points[1].AfterRecordingGap);
    }

    /// <summary>
    /// The defect: ComputePipeline re-runs EnrichPoints after AnomalyCorrector has repaired
    /// the timestamps, and a boundary the repaired timestamps no longer justify must go.
    /// </summary>
    [Fact]
    public void EnrichPoints_RerunAfterTheGapIsClosed_ClearsTheBoundaryItSet()
    {
        var points = ThreePoints(0, 1, 42);
        SpeedCalculator.EnrichPoints(points);
        Assert.True(points[2].AfterRecordingGap);   // arrange, not the assertion under test

        points[2].Time = T0.AddMinutes(2);          // what a timestamp correction does
        SpeedCalculator.EnrichPoints(points);

        Assert.False(points[2].AfterRecordingGap);
        Assert.True(points[2].DistFromPrev > 0);
        Assert.True(points[2].CalcSpeed > 0);
    }

    [Fact]
    public void EnrichPoints_NeverWritesTheSourceSegmentFlag()
    {
        var points = ThreePoints(0, 1, 42);
        points[1].StartsNewSegment = true;

        SpeedCalculator.EnrichPoints(points);

        // Not invented where the source did not have one ...
        Assert.False(points[2].StartsNewSegment);
        // ... and not destroyed where it did.
        Assert.True(points[1].StartsNewSegment);
    }

    // ------------------------------------------------------- ClampSpeeds owns the clamp bit

    [Fact]
    public void ClampSpeeds_RerunAfterTheSpikeIsGone_ClearsTheFlagItSet()
    {
        var points = ThreePoints(0, 1, 2);
        points[2].Lat = 46.0;                       // ~111 km in a minute
        SpeedCalculator.EnrichPoints(points);
        Assert.Equal(1, SpeedCalculator.ClampSpeeds(points, 4.0));
        Assert.True(points[2].SpeedClamped);        // arrange

        points[2].Lat = 45.002;                     // what a position correction does
        SpeedCalculator.EnrichPoints(points);
        Assert.Equal(0, SpeedCalculator.ClampSpeeds(points, 4.0));

        Assert.False(points[2].SpeedClamped);
        Assert.True(points[2].DistFromPrev > 0);
    }

    /// <summary>
    /// An implausible speed means the DISTANCE between two fixes is untrustworthy. It does not
    /// mean the recorder was off: those seconds still elapsed and still belong to the recording,
    /// so clamping must not remove them from recorded (hence moving) time.
    /// </summary>
    [Fact]
    public void ClampSpeeds_DoesNotTurnTheIntervalIntoUnrecordedTime()
    {
        var points = ThreePoints(0, 1, 2);
        points[2].Lat = 46.0;
        SpeedCalculator.EnrichPoints(points);
        SpeedCalculator.ClampSpeeds(points, 4.0);

        Assert.True(points[2].SpeedClamped);
        Assert.True(points[2].BreaksPath);           // the distance is gone ...
        Assert.False(points[2].BreaksRecordedTime);  // ... but the two minutes are not
    }

    [Fact]
    public void ClampSpeeds_NeverWritesTheSourceSegmentFlag()
    {
        var points = ThreePoints(0, 1, 2);
        points[2].Lat = 46.0;
        SpeedCalculator.EnrichPoints(points);
        SpeedCalculator.ClampSpeeds(points, 4.0);

        Assert.False(points[2].StartsNewSegment);
    }

    // ------------------------------------------------------------------ end to end

    /// <summary>
    /// A backward timestamp makes the NEXT interval look like a 12-minute recording gap. The
    /// pipeline detects it, rewrites the offending timestamp to previous + 1s, and re-derives
    /// everything downstream - at which point the gap is gone and so is the boundary.
    ///
    /// Before the fix this track reported total 300s / stopped 0s / moving 181s - the 119
    /// seconds behind the stale boundary vanished from moving time while nothing was stopped -
    /// and 40 m of climb instead of 50, because the elevation profile was still being split
    /// into two sections at the same stale boundary.
    /// </summary>
    [Fact]
    public void Compute_FixAnomalies_ReDerivesBoundariesFromTheCorrectedTrack()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 45.00000, Lon = 6.0, Ele = 100.0, Time = T0 },
            new() { Lat = 45.00002, Lon = 6.0, Ele = 100.5, Time = T0.AddMinutes(-10) },  // backward
            new() { Lat = 45.00100, Lon = 6.0, Ele = 110.0, Time = T0.AddMinutes(2) },
            new() { Lat = 45.00200, Lon = 6.0, Ele = 120.0, Time = T0.AddMinutes(3) },
            new() { Lat = 45.00300, Lon = 6.0, Ele = 130.0, Time = T0.AddMinutes(4) },
            new() { Lat = 45.00400, Lon = 6.0, Ele = 150.0, Time = T0.AddMinutes(5) },
        };

        var cfg = new ComputeConfig
        {
            ElevationThreshold = 2.0,
            SmoothingLevel = "none",
            StopConfig = StopDetector.Presets[StopDetector.PresetHiking],
            MaxReasonableSpeed = SpeedCalculator.PresetMaxSpeed[StopDetector.PresetHiking],
            AnomalyConfig = AnomalyConfig.Default(),
            FixAnomalies = true,
        };

        var (summary, processed) = ComputePipeline.Compute(points, 1, cfg);

        Assert.True(summary.AnomalyReport!.CorrectionApplied);
        Assert.Equal(TimeSpan.FromMinutes(5), summary.TotalTime);
        Assert.Equal(TimeSpan.Zero, summary.StoppedTime);
        // Nothing was stopped and the recording never actually broke, so every recorded
        // second is moving time.
        Assert.Equal(summary.TotalTime, summary.MovingTime);
        // One continuous climb, not two sections split at a boundary that no longer exists.
        Assert.Equal(50, summary.Elevation.Gain, 6);
        Assert.All(processed, p => Assert.False(p.AfterRecordingGap));
    }
}
