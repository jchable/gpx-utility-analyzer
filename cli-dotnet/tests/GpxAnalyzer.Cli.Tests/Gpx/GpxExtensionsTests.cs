using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Tests.Gpx;

public class GpxExtensionsTests
{
    private static string TestDataPath(string name) =>
        Path.Combine("testdata", name);

    [Fact]
    public void ParseFile_WithExtensions_ParsesHeartRate()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-extensions.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(120, points[0].HeartRate);
        Assert.Equal(135, points[1].HeartRate);
        Assert.Equal(170, points[4].HeartRate);
    }

    [Fact]
    public void ParseFile_WithExtensions_ParsesCadence()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-extensions.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(80, points[0].Cadence);
        Assert.Equal(95, points[3].Cadence);
    }

    [Fact]
    public void ParseFile_WithExtensions_ParsesTemperature()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-extensions.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(18.5, points[0].Temperature);
        Assert.Equal(20.5, points[4].Temperature);
    }

    [Fact]
    public void ParseFile_WithExtensions_ParsesPower()
    {
        var doc = GpxParser.ParseFile(TestDataPath("with-extensions.gpx"));
        var points = doc.AllPoints();
        Assert.Equal(200, points[0].Power);
        Assert.Equal(280, points[3].Power);
    }

    [Fact]
    public void ParseFile_SmallGpx_NoExtensions()
    {
        var doc = GpxParser.ParseFile(TestDataPath("small.gpx"));
        var points = doc.AllPoints();
        Assert.Null(points[0].HeartRate);
        Assert.Null(points[0].Cadence);
        Assert.Null(points[0].Power);
        Assert.Null(points[0].Temperature);
    }
}
