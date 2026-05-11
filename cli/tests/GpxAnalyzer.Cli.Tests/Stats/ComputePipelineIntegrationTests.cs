using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

/// <summary>
/// Integration tests for ComputePipeline exercising the full pipeline
/// with various configurations, including anomaly detection and correction.
/// </summary>
public class ComputePipelineIntegrationTests
{
    private static DateTime T(int seconds) =>
        DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime().AddSeconds(seconds);

    // ---------------------------------------------------------------
    // Pipeline with FixAnomalies: the full path that caused the 699 km/h bug
    // ---------------------------------------------------------------

    [Fact]
    public void Compute_WithFixAnomaliesAndOutlier_MaxSpeedClamped()
    {
        // Simulate: normal track with a GPS jump that passes FilterOutliers
        // but produces high speed after EnrichPoints recalculation in RecalculateStats.
        // A frozen section triggers anomaly correction → RecalculateStats.
        var points = BuildTrackWithFrozenSectionAndSpeedArtifact();

        var cfg = new ComputeConfig
        {
            MaxReasonableSpeed = 7.0,  // 25.2 km/h (running preset)
            StopConfig = StopDetector.Presets["running"],
            AnomalyConfig = new AnomalyConfig
            {
                GpsFrozenMinPoints = 3,
                GpsFrozenEpsilon = 0.000001,
            },
            FixAnomalies = true,
        };

        var (summary, processed) = ComputePipeline.Compute(points, 1, cfg);

        Assert.True(summary.Speed.MaxSpeed <= 7.0,
            $"MaxSpeed should be clamped to 7.0 m/s after anomaly correction, got {summary.Speed.MaxSpeed:F1} m/s ({summary.Speed.MaxSpeed * 3.6:F1} km/h)");
    }

    [Fact]
    public void Compute_WithFixAnomalies_DistanceNotInflated()
    {
        var points = BuildTrackWithFrozenSectionAndSpeedArtifact();

        var cfg = new ComputeConfig
        {
            MaxReasonableSpeed = 7.0,
            StopConfig = StopDetector.Presets["running"],
            AnomalyConfig = new AnomalyConfig
            {
                GpsFrozenMinPoints = 3,
                GpsFrozenEpsilon = 0.000001,
            },
            FixAnomalies = true,
        };

        var (summary, _) = ComputePipeline.Compute(points, 1, cfg);

        // Distance should be reasonable (not inflated by GPS jumps)
        // ~12 normal points × ~131m = ~1.6 km + frozen estimation
        Assert.True(summary.TotalDistance < 5000,
            $"Distance should be reasonable, got {summary.TotalDistance:F0}m");
    }

    [Fact]
    public void Compute_WithFixAnomaliesDisabled_StillFiltersOutliers()
    {
        var points = BuildTrackWithOutlier();

        var cfg = new ComputeConfig
        {
            MaxReasonableSpeed = 7.0,
            StopConfig = StopDetector.Presets["running"],
            AnomalyConfig = AnomalyConfig.Default(),
            FixAnomalies = false,
        };

        var (summary, _) = ComputePipeline.Compute(points, 1, cfg);

        // FilterOutliers + ClampSpeeds should still work even without fix-anomalies
        Assert.True(summary.Speed.MaxSpeed <= 7.0,
            $"MaxSpeed should be clamped even without FixAnomalies, got {summary.Speed.MaxSpeed * 3.6:F1} km/h");
    }

    // ---------------------------------------------------------------
    // Pipeline with GPS outlier removal
    // ---------------------------------------------------------------

    [Fact]
    public void Compute_WithOutlier_FiltersAndClampsCorrectly()
    {
        var points = BuildTrackWithOutlier();

        var cfg = new ComputeConfig
        {
            MaxReasonableSpeed = 7.0,
        };

        var (summary, processed) = ComputePipeline.Compute(points, 1, cfg);

        Assert.True(summary.FilteredPoints > 0, "Should have filtered outlier points");
        Assert.True(summary.Speed.MaxSpeed <= 7.0,
            $"MaxSpeed should be clamped, got {summary.Speed.MaxSpeed * 3.6:F1} km/h");
        Assert.True(processed.Count < points.Count, "Processed points should be fewer after filtering");
    }

    [Fact]
    public void Compute_WithoutMaxReasonableSpeed_NoFiltering()
    {
        var points = BuildTrackWithOutlier();

        var cfg = new ComputeConfig
        {
            MaxReasonableSpeed = 0,  // Disabled
        };

        var (summary, processed) = ComputePipeline.Compute(points, 1, cfg);

        Assert.Equal(0, summary.FilteredPoints);
        Assert.Equal(points.Count, processed.Count);
    }

    // ---------------------------------------------------------------
    // Pipeline edge cases
    // ---------------------------------------------------------------

    [Fact]
    public void Compute_EmptyPoints_ReturnsEmptySummary()
    {
        var points = new List<TrackPoint>();
        var cfg = ComputeConfig.Default();

        var (summary, processed) = ComputePipeline.Compute(points, 0, cfg);

        Assert.Equal(0, summary.TotalDistance);
        Assert.Equal(0, summary.PointCount);
        Assert.Empty(processed);
    }

    [Fact]
    public void Compute_SinglePoint_ReturnsMinimalSummary()
    {
        var points = new List<TrackPoint>
        {
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
        };
        var cfg = ComputeConfig.Default();

        var (summary, processed) = ComputePipeline.Compute(points, 1, cfg);

        Assert.Equal(1, summary.PointCount);
        Assert.Equal(0, summary.TotalDistance);
        Assert.Single(processed);
    }

    [Theory]
    [InlineData("hiking", 4.0)]
    [InlineData("trail", 7.0)]
    [InlineData("running", 7.0)]
    [InlineData("cycling", 25.0)]
    [InlineData("walking", 4.0)]
    [InlineData("swimming", 3.0)]
    public void Compute_AllPresets_MaxSpeedRespected(string preset, double maxSpeed)
    {
        var points = BuildTrackWithOutlier();

        var cfg = new ComputeConfig
        {
            MaxReasonableSpeed = maxSpeed,
            StopConfig = StopDetector.Presets[preset],
        };

        var (summary, _) = ComputePipeline.Compute(points, 1, cfg);

        Assert.True(summary.Speed.MaxSpeed <= maxSpeed,
            $"MaxSpeed for preset '{preset}' should be <= {maxSpeed} m/s, got {summary.Speed.MaxSpeed:F1} m/s");
    }

    // ---------------------------------------------------------------
    // Test data builders
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a track with a frozen GPS section (identical positions)
    /// AND a speed artifact (large position jump in short time).
    /// This combination triggers anomaly correction and exposes the
    /// EnrichPoints → ClampSpeeds regression in RecalculateStats.
    /// </summary>
    private static List<TrackPoint> BuildTrackWithFrozenSectionAndSpeedArtifact()
    {
        var points = new List<TrackPoint>();
        int t = 0;

        // 5 normal points
        for (int i = 0; i < 5; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.0 + i * 0.001,
                Lon = 2.0 + i * 0.001,
                Ele = 100,
                Time = T(t),
            });
            t += 60;
        }

        // 5 frozen points (same position, triggers anomaly detection)
        for (int i = 0; i < 5; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.004,
                Lon = 2.004,
                Ele = 100,
                Time = T(t),
            });
            t += 60;
        }

        // After frozen: a sudden jump (GPS artifact)
        // This point survives FilterOutliers but creates high CalcSpeed after EnrichPoints
        points.Add(new TrackPoint
        {
            Lat = 48.01,  // ~660m jump
            Lon = 2.01,
            Ele = 100,
            Time = T(t),
        });
        t += 2; // Very short time → high speed

        // Resume normal
        points.Add(new TrackPoint
        {
            Lat = 48.0101,
            Lon = 2.0101,
            Ele = 100,
            Time = T(t),
        });
        t += 60;

        // More normal points
        for (int i = 0; i < 3; i++)
        {
            points.Add(new TrackPoint
            {
                Lat = 48.011 + i * 0.001,
                Lon = 2.011 + i * 0.001,
                Ele = 100,
                Time = T(t),
            });
            t += 60;
        }

        return points;
    }

    /// <summary>
    /// Creates a simple track with a clear GPS outlier.
    /// </summary>
    private static List<TrackPoint> BuildTrackWithOutlier()
    {
        return
        [
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = T(0) },
            new() { Lat = 48.0001, Lon = 2.0001, Ele = 100, Time = T(10) },
            // GPS jump: ~157 km away in 1 second → 157,000 m/s (way above any threshold)
            new() { Lat = 49.0, Lon = 3.0, Ele = 100, Time = T(11) },
            // Returns near original position
            new() { Lat = 48.0003, Lon = 2.0003, Ele = 100, Time = T(20) },
            new() { Lat = 48.0004, Lon = 2.0004, Ele = 100, Time = T(30) },
            new() { Lat = 48.0005, Lon = 2.0005, Ele = 100, Time = T(40) },
        ];
    }
}
