using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Integration;

public class InProcessAnalysisTests
{
    private static string TestDataPath(string filename) =>
        Path.Combine("testdata", filename);

    [Fact]
    public void AnalyzeGpx_SmallFile_ProducesValidGpxStats()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        Assert.True(points.Count > 0, "GPX should have points");

        var cfg = ComputeConfig.Default();
        var (summary, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        // Verify Summary has reasonable values
        Assert.True(summary.TotalDistance > 0, "Distance should be positive");
        Assert.True(summary.PointCount > 0, "Point count should be positive");
        Assert.True(processed.Count > 0, "Processed points should exist");

        // Map to GpxStats and verify
        var stats = SummaryMapper.ToGpxStats("small.gpx", summary);

        Assert.Equal("small.gpx", stats.Filename);
        Assert.True(stats.TotalDistanceM > 0);
        Assert.True(stats.TotalDistanceKm > 0);
        Assert.Equal(stats.TotalDistanceM / 1000, stats.TotalDistanceKm);
        Assert.True(stats.PointCount > 0);
        Assert.True(stats.SegmentCount >= 1);
        Assert.False(string.IsNullOrEmpty(stats.StartTime));
        Assert.False(string.IsNullOrEmpty(stats.EndTime));
        Assert.True(stats.TotalTime.Seconds > 0);
    }

    [Fact]
    public void AnalyzeGpx_WithEnrichedExport_ProducesValidGpxFile()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        var cfg = ComputeConfig.Default();
        var (_, processed) = ComputePipeline.Compute(points, doc.SegmentCount(), cfg);

        // Export enriched GPX to temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"gpx-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var outPath = Path.Combine(tempDir, "small_processed.gpx");
            GpxWriter.WriteEnriched(outPath, processed, "small");

            // Verify file exists and has content
            Assert.True(File.Exists(outPath), "Enriched GPX file should exist");
            var content = File.ReadAllText(outPath);
            Assert.True(content.Length > 0, "Enriched GPX should have content");
            Assert.Contains("<gpx", content);
            Assert.Contains("<trkpt", content);

            // Verify enriched extensions are present
            Assert.Contains("gpxa:TrackPointMetrics", content);
            Assert.Contains("gpxa:speed", content);
            Assert.Contains("gpxa:dist", content);

            // Verify the enriched GPX can be re-parsed
            var reDoc = GpxParser.ParseFile(outPath);
            var rePoints = reDoc.AllPoints();
            Assert.True(rePoints.Count > 0, "Re-parsed GPX should have points");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
