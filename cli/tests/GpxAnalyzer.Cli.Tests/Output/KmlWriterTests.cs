using System.Xml.Linq;
using GpxAnalyzer.Cli.Core.Output;

namespace GpxAnalyzer.Cli.Tests.Output;

public class KmlWriterTests
{
    private static readonly double[][] SampleCoordinates =
    [
        [6.4, 45.06, 1450],
        [6.41, 45.065, 1550],
        [6.42, 45.07, 1700],
        [6.43, 45.075, 1900],
        [6.46, 45.064, 2642],
    ];

    private static readonly List<(string Name, double Lon, double Lat, string? Description)> SamplePois =
    [
        ("Water Source", 6.41, 45.065, "Fresh water fountain"),
        ("Summit", 6.46, 45.064, null),
    ];

    private static XDocument WriteAndParse(double[][] coords,
        IReadOnlyList<(string, double, double, string?)>? pois = null,
        string name = "Test Track")
    {
        using var ms = new MemoryStream();
        KmlWriter.Write(ms, name, coords, pois);
        ms.Position = 0;
        return XDocument.Load(ms);
    }

    [Fact]
    public void Write_ProducesValidKml()
    {
        var doc = WriteAndParse(SampleCoordinates);
        Assert.NotNull(doc.Root);
        Assert.Equal("kml", doc.Root.Name.LocalName);
    }

    [Fact]
    public void Write_ContainsDocumentName()
    {
        var doc = WriteAndParse(SampleCoordinates, name: "My Route");
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var nameEl = doc.Descendants(ns + "Document").First().Element(ns + "name");
        Assert.Equal("My Route", nameEl?.Value);
    }

    [Fact]
    public void Write_ContainsTrackPlacemark()
    {
        var doc = WriteAndParse(SampleCoordinates);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var placemarks = doc.Descendants(ns + "Placemark").ToList();
        Assert.True(placemarks.Count >= 1, "Should have at least one Placemark (track)");

        var lineStrings = doc.Descendants(ns + "LineString").ToList();
        Assert.Single(lineStrings);
    }

    [Fact]
    public void Write_TrackCoordinatesContainAllPoints()
    {
        var doc = WriteAndParse(SampleCoordinates);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var coords = doc.Descendants(ns + "LineString").First().Element(ns + "coordinates")?.Value ?? "";
        var points = coords.Trim().Split(' ');
        Assert.Equal(SampleCoordinates.Length, points.Length);
    }

    [Fact]
    public void Write_CoordinatesHaveLonLatEleFormat()
    {
        var doc = WriteAndParse(SampleCoordinates);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var coords = doc.Descendants(ns + "LineString").First().Element(ns + "coordinates")?.Value ?? "";
        var firstPoint = coords.Trim().Split(' ')[0];
        var parts = firstPoint.Split(',');
        Assert.Equal(3, parts.Length); // lon,lat,ele
        Assert.Equal("6.4", parts[0]);
        Assert.Equal("45.06", parts[1]);
        Assert.Equal("1450", parts[2]);
    }

    [Fact]
    public void Write_WithPois_CreatesPoiPlacemarks()
    {
        var doc = WriteAndParse(SampleCoordinates, SamplePois);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var placemarks = doc.Descendants(ns + "Placemark").ToList();
        // 1 track placemark + 2 POI placemarks = 3
        Assert.Equal(3, placemarks.Count);
    }

    [Fact]
    public void Write_WithPois_PoiHasNameAndPoint()
    {
        var doc = WriteAndParse(SampleCoordinates, SamplePois);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var poiPlacemarks = doc.Descendants(ns + "Placemark")
            .Where(p => p.Element(ns + "Point") != null)
            .ToList();

        Assert.Equal(2, poiPlacemarks.Count);
        Assert.Equal("Water Source", poiPlacemarks[0].Element(ns + "name")?.Value);
        Assert.Equal("Summit", poiPlacemarks[1].Element(ns + "name")?.Value);
    }

    [Fact]
    public void Write_WithPois_PoiDescriptionIncludedWhenNotNull()
    {
        var doc = WriteAndParse(SampleCoordinates, SamplePois);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var poiPlacemarks = doc.Descendants(ns + "Placemark")
            .Where(p => p.Element(ns + "Point") != null)
            .ToList();

        Assert.Equal("Fresh water fountain", poiPlacemarks[0].Element(ns + "description")?.Value);
        Assert.Null(poiPlacemarks[1].Element(ns + "description"));
    }

    [Fact]
    public void Write_WithoutPois_OnlyTrackPlacemark()
    {
        var doc = WriteAndParse(SampleCoordinates);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var placemarks = doc.Descendants(ns + "Placemark").ToList();
        Assert.Single(placemarks);
    }

    [Fact]
    public void Write_ContainsTrackStyle()
    {
        var doc = WriteAndParse(SampleCoordinates);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var styles = doc.Descendants(ns + "Style").ToList();
        Assert.Single(styles);
        Assert.Equal("trackStyle", styles[0].Attribute("id")?.Value);
    }
}
