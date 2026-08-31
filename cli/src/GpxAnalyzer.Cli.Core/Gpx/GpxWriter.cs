using System.Globalization;
using System.Xml;

namespace GpxAnalyzer.Cli.Core.Gpx;

public static class GpxWriter
{
    private const string GpxNs = "http://www.topografix.com/GPX/1/1";
    private const string GpxaNs = "http://gpx-analyzer.io/extensions/v1";
    private const string GpxtpxNs = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    /// <summary>
    /// Writes a route GPX with track points and optional waypoints (POIs as &lt;wpt&gt;).
    /// </summary>
    public static void WriteRoute(Stream stream, string trackName, double[][] coordinates,
        IReadOnlyList<(string Name, double Lon, double Lat, string? Type)>? waypoints = null)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            CloseOutput = false
        };

        using var w = XmlWriter.Create(stream, settings);

        w.WriteStartDocument();
        w.WriteStartElement("gpx", GpxNs);
        w.WriteAttributeString("version", "1.1");
        w.WriteAttributeString("creator", "gpx-analyzer");

        // Waypoints (POIs)
        if (waypoints is not null)
        {
            foreach (var wp in waypoints)
            {
                w.WriteStartElement("wpt", GpxNs);
                w.WriteAttributeString("lat", wp.Lat.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("lon", wp.Lon.ToString(CultureInfo.InvariantCulture));
                w.WriteElementString("name", GpxNs, wp.Name);
                if (wp.Type is not null)
                    w.WriteElementString("type", GpxNs, wp.Type);
                w.WriteEndElement(); // wpt
            }
        }

        // Track
        w.WriteStartElement("trk", GpxNs);
        w.WriteElementString("name", GpxNs, trackName);

        w.WriteStartElement("trkseg", GpxNs);

        foreach (var coord in coordinates)
        {
            w.WriteStartElement("trkpt", GpxNs);
            w.WriteAttributeString("lat", coord[1].ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("lon", coord[0].ToString(CultureInfo.InvariantCulture));
            if (coord.Length > 2)
                w.WriteElementString("ele", GpxNs, coord[2].ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement(); // trkpt
        }

        w.WriteEndElement(); // trkseg
        w.WriteEndElement(); // trk
        w.WriteEndElement(); // gpx
        w.WriteEndDocument();
    }

    public static void Write(string path, List<TrackPoint> points, string trackName)
    {
        EnsureDirectory(path);
        WriteToFile(path, points, trackName, enrich: false);
    }

    public static void WriteEnriched(string path, List<TrackPoint> points, string trackName)
    {
        EnsureDirectory(path);
        WriteToFile(path, points, trackName, enrich: true);
    }

    /// <summary>
    /// Creates the output directory when the path has one. Path.GetDirectoryName
    /// returns string.Empty (not null) for a bare filename, so a "?? \".\"" fallback
    /// never fires and CreateDirectory("") throws.
    /// </summary>
    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static void WriteToFile(string path, List<TrackPoint> points, string trackName, bool enrich)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            CloseOutput = true
        };

        using var stream = File.Create(path);
        using var w = XmlWriter.Create(stream, settings);

        w.WriteStartDocument();
        w.WriteStartElement("gpx", GpxNs);
        w.WriteAttributeString("version", "1.1");
        w.WriteAttributeString("creator", "gpx-analyzer");
        w.WriteAttributeString("xmlns", "gpxtpx", null, GpxtpxNs);
        if (enrich)
            w.WriteAttributeString("xmlns", "gpxa", null, GpxaNs);

        w.WriteStartElement("trk", GpxNs);
        w.WriteElementString("name", GpxNs, trackName);

        w.WriteStartElement("trkseg", GpxNs);

        double cumDist = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var tp = points[i];

            if (i > 0)
            {
                cumDist += Stats.DistanceCalculator.Haversine(
                    points[i - 1].Lat, points[i - 1].Lon,
                    tp.Lat, tp.Lon);
            }

            w.WriteStartElement("trkpt", GpxNs);
            w.WriteAttributeString("lat", tp.Lat.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("lon", tp.Lon.ToString(CultureInfo.InvariantCulture));
            w.WriteElementString("ele", GpxNs, tp.Ele.ToString(CultureInfo.InvariantCulture));
            // ':' is the time-separator placeholder in a custom format string, so
            // without InvariantCulture this emits e.g. 2024-01-02T10.04.05Z under
            // fi-FI / cs-CZ / et-EE — invalid per the GPX xsd:dateTime schema.
            w.WriteElementString("time", GpxNs,
                tp.Time.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

            // GPS quality lives on the trkpt itself in GPX 1.1 and is part of the
            // source data, not a computed metric — split and merge have no --enrich
            // flag, so writing it only under `enrich` silently discarded it.
            if (tp.Fix is not null)
                w.WriteElementString("fix", GpxNs, tp.Fix);
            if (tp.Satellites is not null)
                w.WriteElementString("sat", GpxNs, tp.Satellites.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Hdop is not null)
                w.WriteElementString("hdop", GpxNs, tp.Hdop.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Vdop is not null)
                w.WriteElementString("vdop", GpxNs, tp.Vdop.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Pdop is not null)
                w.WriteElementString("pdop", GpxNs, tp.Pdop.Value.ToString(CultureInfo.InvariantCulture));

            if (enrich)
            {
                double grade = 0;
                if (i > 0)
                {
                    double hDist = tp.DistFromPrev;
                    if (hDist > 0)
                        grade = (tp.Ele - points[i - 1].Ele) / hDist;
                }

                WriteEnrichedExtensions(w, tp, cumDist, grade);
            }
            else
            {
                WriteSourceExtensions(w, tp);
            }

            w.WriteEndElement(); // trkpt
        }

        w.WriteEndElement(); // trkseg
        w.WriteEndElement(); // trk
        w.WriteEndElement(); // gpx
        w.WriteEndDocument();
    }

    private static void WriteEnrichedExtensions(XmlWriter w, TrackPoint tp,
        double cumDist, double grade)
    {
        w.WriteStartElement("extensions", GpxNs);

        // gpxa:TrackPointMetrics
        w.WriteStartElement("TrackPointMetrics", GpxaNs);
        w.WriteElementString("speed", GpxaNs, tp.CalcSpeed.ToString(CultureInfo.InvariantCulture));
        w.WriteElementString("dist", GpxaNs, cumDist.ToString(CultureInfo.InvariantCulture));
        w.WriteElementString("grade", GpxaNs, grade.ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement(); // TrackPointMetrics

        // Garmin biometrics
        if (tp.HeartRate != null || tp.Cadence != null || tp.Temperature != null)
        {
            w.WriteStartElement("TrackPointExtension", GpxtpxNs);
            if (tp.HeartRate != null)
                w.WriteElementString("hr", GpxtpxNs, tp.HeartRate.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Cadence != null)
                w.WriteElementString("cad", GpxtpxNs, tp.Cadence.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Temperature != null)
                w.WriteElementString("atemp", GpxtpxNs, tp.Temperature.Value.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement(); // TrackPointExtension
        }

        WritePower(w, tp);

        w.WriteEndElement(); // extensions
    }

    /// <summary>
    /// Writes the source biometrics for a non-enriched export. split and merge use
    /// this writer and have no --enrich flag, so without it every heart rate,
    /// cadence, power and temperature sample in the input is silently dropped.
    /// </summary>
    private static void WriteSourceExtensions(XmlWriter w, TrackPoint tp)
    {
        bool hasGarmin = tp.HeartRate is not null || tp.Cadence is not null
                      || tp.Temperature is not null || tp.DeviceSpeed is not null
                      || tp.WaterTemp is not null;
        if (!hasGarmin && tp.Power is null) return;

        w.WriteStartElement("extensions", GpxNs);

        if (hasGarmin)
        {
            w.WriteStartElement("TrackPointExtension", GpxtpxNs);
            if (tp.HeartRate is not null)
                w.WriteElementString("hr", GpxtpxNs, tp.HeartRate.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Cadence is not null)
                w.WriteElementString("cad", GpxtpxNs, tp.Cadence.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.Temperature is not null)
                w.WriteElementString("atemp", GpxtpxNs, tp.Temperature.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.WaterTemp is not null)
                w.WriteElementString("wtemp", GpxtpxNs, tp.WaterTemp.Value.ToString(CultureInfo.InvariantCulture));
            if (tp.DeviceSpeed is not null)
                w.WriteElementString("speed", GpxtpxNs, tp.DeviceSpeed.Value.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }

        WritePower(w, tp);

        w.WriteEndElement(); // extensions
    }

    /// <summary>
    /// Power is written in the GPX default namespace, explicitly. The two-argument
    /// WriteElementString overload passes null for the namespace, which XmlWriter
    /// reads as "inherit the in-scope default" rather than "no namespace", so the
    /// element was already landing in GpxNs while the code claimed it was bare —
    /// and ProfileComputationService looked it up with an unqualified XName and
    /// always got null.
    /// </summary>
    private static void WritePower(XmlWriter w, TrackPoint tp)
    {
        if (tp.Power is null) return;
        w.WriteElementString("power", GpxNs, tp.Power.Value.ToString(CultureInfo.InvariantCulture));
    }
}
