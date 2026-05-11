using System.Globalization;
using System.Xml;

namespace GpxAnalyzer.Cli.Core.Output;

public static class KmlWriter
{
    /// <summary>
    /// Writes a KML file with a track line and optional POI placemarks.
    /// </summary>
    /// <param name="stream">Output stream.</param>
    /// <param name="name">Document/track name.</param>
    /// <param name="coordinates">Track coordinates as [lon, lat, ele] arrays.</param>
    /// <param name="pois">Optional POIs as (name, lon, lat) tuples.</param>
    public static void Write(Stream stream, string name, double[][] coordinates,
        IReadOnlyList<(string Name, double Lon, double Lat, string? Description)>? pois = null)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            CloseOutput = false
        };

        using var w = XmlWriter.Create(stream, settings);

        w.WriteStartDocument();
        w.WriteStartElement("kml", "http://www.opengis.net/kml/2.2");
        w.WriteStartElement("Document");
        w.WriteElementString("name", name);

        // Track style
        w.WriteStartElement("Style");
        w.WriteAttributeString("id", "trackStyle");
        w.WriteStartElement("LineStyle");
        w.WriteElementString("color", "ffff6400"); // AABBGGRR — cyan-ish
        w.WriteElementString("width", "3");
        w.WriteEndElement(); // LineStyle
        w.WriteEndElement(); // Style

        // Track placemark
        w.WriteStartElement("Placemark");
        w.WriteElementString("name", name);
        w.WriteElementString("styleUrl", "#trackStyle");

        w.WriteStartElement("LineString");
        w.WriteElementString("altitudeMode", "clampToGround");

        // Build coordinate string: lon,lat,ele separated by spaces
        var coordStr = string.Join(" ",
            coordinates.Select(c =>
            {
                var lon = c[0].ToString(CultureInfo.InvariantCulture);
                var lat = c[1].ToString(CultureInfo.InvariantCulture);
                var ele = c.Length > 2 ? c[2].ToString(CultureInfo.InvariantCulture) : "0";
                return $"{lon},{lat},{ele}";
            }));

        w.WriteElementString("coordinates", coordStr);
        w.WriteEndElement(); // LineString
        w.WriteEndElement(); // Placemark (track)

        // POI placemarks
        if (pois is not null)
        {
            foreach (var poi in pois)
            {
                w.WriteStartElement("Placemark");
                w.WriteElementString("name", poi.Name);
                if (poi.Description is not null)
                    w.WriteElementString("description", poi.Description);

                w.WriteStartElement("Point");
                var poiCoord = $"{poi.Lon.ToString(CultureInfo.InvariantCulture)},{poi.Lat.ToString(CultureInfo.InvariantCulture)},0";
                w.WriteElementString("coordinates", poiCoord);
                w.WriteEndElement(); // Point
                w.WriteEndElement(); // Placemark
            }
        }

        w.WriteEndElement(); // Document
        w.WriteEndElement(); // kml
        w.WriteEndDocument();
    }
}
