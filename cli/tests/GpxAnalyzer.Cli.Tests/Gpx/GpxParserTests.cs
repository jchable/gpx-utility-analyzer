using GpxAnalyzer.Cli.Core.Gpx;

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

    [Fact]
    public void ParseFile_WithTrackType_ParsesType()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-gps-quality.gpx"));
        Assert.Equal("running", doc.Tracks[0].Type);
    }

    [Fact]
    public void ParseFile_SmallGpx_TrackTypeIsNull()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        Assert.Null(doc.Tracks[0].Type);
    }

    [Fact]
    public void ParseFile_WithGpsQuality_ParsesFix()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-gps-quality.gpx"));
        var points = doc.AllPoints();
        Assert.Equal("3d", points[0].Fix);
        Assert.Equal("2d", points[2].Fix);
    }

    [Fact]
    public void ParseFile_WithGpsQuality_ParsesSatellites()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-gps-quality.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(12, points[0].Satellites);
        Assert.Equal(4, points[2].Satellites);
    }

    [Fact]
    public void ParseFile_WithGpsQuality_ParsesHdop()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-gps-quality.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(0.8, points[0].Hdop);
        Assert.Equal(8.0, points[2].Hdop);
    }

    [Fact]
    public void ParseFile_WithGpsQuality_ParsesVdopAndPdop()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-gps-quality.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(1.2, points[0].Vdop);
        Assert.Equal(1.4, points[0].Pdop);
        Assert.Null(points[2].Vdop);
        Assert.Null(points[2].Pdop);
    }

    [Fact]
    public void ParseFile_SmallGpx_GpsQualityFieldsAreNull()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        Assert.Null(points[0].Fix);
        Assert.Null(points[0].Satellites);
        Assert.Null(points[0].Hdop);
        Assert.Null(points[0].Vdop);
        Assert.Null(points[0].Pdop);
    }
}
