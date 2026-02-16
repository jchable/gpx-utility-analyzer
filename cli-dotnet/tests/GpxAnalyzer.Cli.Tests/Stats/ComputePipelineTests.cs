using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Stats;

public class ComputePipelineTests
{
    private static string TestDataPath(string name) =>
        Path.Combine("testdata", name);

    [Fact]
    public void Compute_SmallGpx_ReturnsValidSummary()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        Assert.True(summary.TotalDistance > 0);
        Assert.True(summary.TotalDistance3D > 0);
        Assert.Equal(5, summary.PointCount);
        Assert.Equal(1, summary.SegmentCount);
        Assert.True(summary.MovingTime > TimeSpan.Zero);
        Assert.True(summary.Speed.AvgSpeed > 0);
    }

    [Fact]
    public void Compute_SmallGpx_ProcessedPointCount()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (_, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        // All points should survive (no outliers in small.gpx)
        Assert.Equal(5, processed.Count);
    }

    [Fact]
    public void Compute_TwoSegments_HandlesMultipleSegments()
    {
        var doc = GpxParser.ParseFile(TestDataPath("two-segments.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        Assert.Equal(2, summary.SegmentCount);
        Assert.Equal(4, summary.PointCount);
    }

    [Fact]
    public void Compute_WithExtensions_IncludesBiometrics()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-extensions.gpx"));
        var points = doc.AllPoints();
        var cfg = new ComputeConfig { BiometricsCfg = new BiometricsConfig { MaxHR = 190 } };
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        Assert.NotNull(summary.Biometrics.HeartRate);
        Assert.True(summary.Biometrics.HeartRate!.Avg > 0);
        Assert.NotNull(summary.Biometrics.Power);
        Assert.NotNull(summary.Biometrics.Cadence);
        Assert.NotNull(summary.Biometrics.Temperature);
    }

    [Fact]
    public void Compute_DefaultConfig_UsesCorrectDefaults()
    {
        var cfg = ComputeConfig.Default();
        Assert.Equal(2.0, cfg.ElevationThreshold);
        Assert.Equal("medium", cfg.SmoothingLevel);
        Assert.Equal(ElevationAlgo.Threshold, cfg.ElevationCfg.Algo);
        Assert.Equal("none", cfg.TrackSmoothing);
    }

    [Fact]
    public void Compute_DifferentAlgos_AllWork()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();

        foreach (var algo in new[] { ElevationAlgo.Threshold, ElevationAlgo.DouglasPeucker, ElevationAlgo.Segments })
        {
            var cfg = new ComputeConfig
            {
                ElevationCfg = new ElevationConfig
                {
                    Algo = algo,
                    Threshold = 2.0,
                    Epsilon = 3.0,
                    MinSegLen = 200.0,
                    MaxSlopeDev = 2.0,
                },
            };
            var (summary, _) = ComputePipeline.Compute(new List<TrackPoint>(points.Select(p => new TrackPoint
            {
                Lat = p.Lat, Lon = p.Lon, Ele = p.Ele, Time = p.Time, Speed = p.Speed
            })), doc.SegmentCount(), cfg);

            Assert.True(summary.TotalDistance > 0, $"Algo {algo} failed");
        }
    }
}
