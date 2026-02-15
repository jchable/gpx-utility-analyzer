using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Tests.Gpx;

public class GpxParserTests
{
    private static string TestDataPath(string name) =>
        Path.Combine("testdata", name);

    [Fact]
    public void ParseFile_SmallGpx_ReturnsCorrectPointCount()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        Assert.Equal(5, doc.PointCount());
    }

    [Fact]
    public void ParseFile_SmallGpx_ReturnsOneSegment()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        Assert.Equal(1, doc.SegmentCount());
    }

    [Fact]
    public void ParseFile_SmallGpx_ParsesCoordinates()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(48.8566, points[0].Lat, 4);
        Assert.Equal(2.3522, points[0].Lon, 4);
        Assert.Equal(35.0, points[0].Ele, 1);
    }

    [Fact]
    public void ParseFile_SmallGpx_ParsesTime()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc), points[0].Time);
    }

    [Fact]
    public void ParseFile_SmallGpx_ParsesSpeed()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(1.5, points[0].Speed, 1);
    }

    [Fact]
    public void ParseFile_TwoSegments_ReturnsCorrectSegmentCount()
    {
        var doc = GpxParser.ParseFile(TestDataPath("two-segments.gpx"));
        Assert.Equal(2, doc.SegmentCount());
        Assert.Equal(4, doc.PointCount());
    }

    [Fact]
    public void ParseFile_TwoSegments_AllPointsReturnsAllPoints()
    {
        var doc = GpxParser.ParseFile(TestDataPath("two-segments.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(4, points.Count);
        // First segment ends at 48.8580, second starts at 48.8600
        Assert.Equal(48.8580, points[1].Lat, 4);
        Assert.Equal(48.8600, points[2].Lat, 4);
    }

    [Fact]
    public void ParseFile_NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => GpxParser.ParseFile("nonexistent.gpx"));
    }
}
