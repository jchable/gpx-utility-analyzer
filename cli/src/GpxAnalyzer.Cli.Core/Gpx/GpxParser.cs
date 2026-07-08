using System.Globalization;
using System.Xml.Linq;

namespace GpxAnalyzer.Cli.Core.Gpx;

public static class GpxParser
{
    public static GpxDocument ParseFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    public static GpxDocument Parse(Stream stream)
    {
        var xdoc = XDocument.Load(stream);
        var root = xdoc.Root ?? throw new InvalidOperationException("Empty GPX document");
        var ns = root.Name.Namespace;

        var doc = new GpxDocument
        {
            Version = root.Attribute("version")?.Value ?? "",
            Creator = root.Attribute("creator")?.Value ?? "",
            Tracks = root.Elements(ns + "trk").Select(trk => ParseTrack(trk, ns)).ToList()
        };

        if (doc.PointCount() == 0)
            throw new InvalidOperationException("No trackpoints found in GPX data");

        return doc;
    }

    private static GpxTrack ParseTrack(XElement trk, XNamespace ns) => new()
    {
        Name = trk.Element(ns + "name")?.Value ?? "",
        Desc = trk.Element(ns + "desc")?.Value ?? "",
        Type = trk.Element(ns + "type")?.Value,
        Segments = trk.Elements(ns + "trkseg").Select(seg => ParseSegment(seg, ns)).ToList()
    };

    private static GpxSegment ParseSegment(XElement seg, XNamespace ns) => new()
    {
        Points = seg.Elements(ns + "trkpt").Select(pt => ParsePoint(pt, ns)).ToList()
    };

    private static TrackPoint ParsePoint(XElement pt, XNamespace ns)
    {
        var lat = double.Parse(pt.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture);
        var lon = double.Parse(pt.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture);

        var eleStr = pt.Element(ns + "ele")?.Value;
        var ele = eleStr != null
            ? double.Parse(eleStr, CultureInfo.InvariantCulture)
            : 0.0;

        var timeStr = pt.Element(ns + "time")?.Value;
        var time = timeStr != null
            ? DateTime.Parse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            : DateTime.MinValue;

        var speedStr = pt.Element(ns + "speed")?.Value;
        var speed = speedStr != null
            ? double.Parse(speedStr, CultureInfo.InvariantCulture)
            : 0.0;

        // GPS quality fields (direct children of <trkpt>)
        var fix = pt.Element(ns + "fix")?.Value;
        var satellites = ParseInt(pt.Element(ns + "sat")?.Value);
        var hdop = ParseDouble(pt.Element(ns + "hdop")?.Value);
        var vdop = ParseDouble(pt.Element(ns + "vdop")?.Value);
        var pdop = ParseDouble(pt.Element(ns + "pdop")?.Value);

        // Parse extensions
        var extElem = pt.Element(ns + "extensions");
        string? innerXml = null;
        if (extElem != null)
        {
            using var reader = extElem.CreateReader();
            reader.MoveToContent();
            innerXml = reader.ReadInnerXml();
        }

        var ext = GpxExtensionParser.Parse(innerXml);

        return new TrackPoint
        {
            Lat = lat,
            Lon = lon,
            Ele = ele,
            Time = time,
            Speed = speed,
            HeartRate = ext.HeartRate,
            Cadence = ext.Cadence,
            Power = ext.Power,
            Temperature = ext.Temperature,
            Fix = fix,
            Satellites = satellites,
            Hdop = hdop,
            Vdop = vdop,
            Pdop = pdop,
            DeviceSpeed = ext.DeviceSpeed,
            WaterTemp = ext.WaterTemp
        };
    }

    private static int? ParseInt(string? value) =>
        value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;

    private static double? ParseDouble(string? value) =>
        value != null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}
