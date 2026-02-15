using System.Globalization;
using System.Xml;

namespace GpxAnalyzer.Cli.Gpx;

public static class GpxWriter
{
    private const string GpxNs = "http://www.topografix.com/GPX/1/1";
    private const string GpxaNs = "http://gpx-analyzer.io/extensions/v1";
    private const string GpxtpxNs = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    public static void Write(string path, List<TrackPoint> points, string trackName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        WriteToFile(path, points, trackName, enrich: false);
    }

    public static void WriteEnriched(string path, List<TrackPoint> points, string trackName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        WriteToFile(path, points, trackName, enrich: true);
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
            w.WriteElementString("time", GpxNs, tp.Time.ToString("yyyy-MM-ddTHH:mm:ssZ"));

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

        // Power (bare element)
        if (tp.Power != null)
            w.WriteElementString("power", tp.Power.Value.ToString(CultureInfo.InvariantCulture));

        w.WriteEndElement(); // extensions
    }
}
