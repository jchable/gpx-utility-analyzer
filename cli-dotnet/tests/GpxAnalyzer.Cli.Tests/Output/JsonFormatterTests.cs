using System.Text.Json;
using GpxAnalyzer.Cli.Gpx;
using GpxAnalyzer.Cli.Output;
using GpxAnalyzer.Cli.Stats;

namespace GpxAnalyzer.Cli.Tests.Output;

public class JsonFormatterTests
{
    private static string TestDataPath(string name) =>
        Path.Combine("testdata", name);

    [Fact]
    public void Format_SmallGpx_ProducesValidJson()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        var formatter = new JsonFormatter();
        using var sw = new StringWriter();
        formatter.Format(sw, "test.gpx", summary, cfg.StopConfig);

        var json = sw.ToString();
        Assert.False(string.IsNullOrWhiteSpace(json));

        // Should parse as valid JSON
        var doc2 = JsonDocument.Parse(json);
        Assert.NotNull(doc2);
    }

    [Fact]
    public void Format_SmallGpx_ContainsExpectedFields()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        var formatter = new JsonFormatter();
        using var sw = new StringWriter();
        formatter.Format(sw, "test.gpx", summary, cfg.StopConfig);

        var json = sw.ToString();
        var jdoc = JsonDocument.Parse(json);
        var root = jdoc.RootElement;

        Assert.True(root.TryGetProperty("filename", out _));
        Assert.True(root.TryGetProperty("total_distance_m", out _));
        Assert.True(root.TryGetProperty("total_distance_km", out _));
        Assert.True(root.TryGetProperty("elevation_gain_m", out _));
        Assert.True(root.TryGetProperty("start_time", out _));
        Assert.True(root.TryGetProperty("total_time", out _));
        Assert.True(root.TryGetProperty("moving_time", out _));
        Assert.True(root.TryGetProperty("avg_speed_kmh", out _));
        Assert.True(root.TryGetProperty("point_count", out _));
    }

    [Fact]
    public void Format_SmallGpx_DurationHasDisplayAndSeconds()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        var formatter = new JsonFormatter();
        using var sw = new StringWriter();
        formatter.Format(sw, "test.gpx", summary, cfg.StopConfig);

        var jdoc = JsonDocument.Parse(sw.ToString());
        var totalTime = jdoc.RootElement.GetProperty("total_time");
        Assert.True(totalTime.TryGetProperty("display", out _));
        Assert.True(totalTime.TryGetProperty("seconds", out _));
    }

    [Fact]
    public void Format_WithExtensions_IncludesBiometrics()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-extensions.gpx"));
        var points = doc.AllPoints();
        var cfg = new ComputeConfig { BiometricsCfg = new BiometricsConfig { MaxHR = 190 } };
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        var formatter = new JsonFormatter();
        using var sw = new StringWriter();
        formatter.Format(sw, "test.gpx", summary, cfg.StopConfig);

        var jdoc = JsonDocument.Parse(sw.ToString());
        var root = jdoc.RootElement;

        Assert.True(root.TryGetProperty("heart_rate", out _));
        Assert.True(root.TryGetProperty("power", out _));
        Assert.True(root.TryGetProperty("cadence", out _));
        Assert.True(root.TryGetProperty("temperature", out _));
    }

    [Fact]
    public void Format_SmallGpx_NoFilteredPointsField()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        var formatter = new JsonFormatter();
        using var sw = new StringWriter();
        formatter.Format(sw, "test.gpx", summary, cfg.StopConfig);

        var jdoc = JsonDocument.Parse(sw.ToString());
        // No outliers were filtered, so filtered_points should be 0/absent
        if (jdoc.RootElement.TryGetProperty("filtered_points", out var fp))
        {
            Assert.Equal(0, fp.GetInt32());
        }
    }

    [Fact]
    public void Format_UsesTwoSpaceIndent()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (summary, _) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        var formatter = new JsonFormatter();
        using var sw = new StringWriter();
        formatter.Format(sw, "test.gpx", summary, cfg.StopConfig);

        var json = sw.ToString();
        // Should contain 2-space indent (matching Go's encoding/json)
        Assert.Contains("  \"filename\"", json);
    }
}
