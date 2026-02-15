using System.Globalization;
using System.Xml.Linq;

namespace GpxAnalyzer.Cli.Gpx;

public static class GpxWriter
{
    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";
    private static readonly XNamespace GpxaNs = "http://gpx-analyzer.io/extensions/v1";
    private static readonly XNamespace GpxtpxNs = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    public static void Write(string path, List<TrackPoint> points, string trackName)
    {
        var gpx = BuildGpx(points, trackName, enrich: false);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        gpx.Save(path);
    }

    public static void WriteEnriched(string path, List<TrackPoint> points, string trackName)
    {
        var gpx = BuildGpx(points, trackName, enrich: true);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        gpx.Save(path);
    }

    private static XDocument BuildGpx(List<TrackPoint> points, string trackName, bool enrich)
    {
        double cumDist = 0;
        var trkpts = new List<XElement>(points.Count);

        for (int i = 0; i < points.Count; i++)
        {
            var tp = points[i];

            if (i > 0)
            {
                cumDist += Stats.DistanceCalculator.Haversine(
                    points[i - 1].Lat, points[i - 1].Lon,
                    tp.Lat, tp.Lon);
            }

            var pt = new XElement(GpxNs + "trkpt",
                new XAttribute("lat", tp.Lat.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("lon", tp.Lon.ToString(CultureInfo.InvariantCulture)),
                new XElement(GpxNs + "ele", tp.Ele.ToString(CultureInfo.InvariantCulture)),
                new XElement(GpxNs + "time", tp.Time.ToString("yyyy-MM-ddTHH:mm:ssZ")));

            if (enrich)
            {
                double grade = 0;
                if (i > 0)
                {
                    double hDist = tp.DistFromPrev;
                    if (hDist > 0)
                        grade = (tp.Ele - points[i - 1].Ele) / hDist;
                }

                var extensions = BuildEnrichedExtensions(tp.CalcSpeed, cumDist, grade, tp);
                if (extensions != null)
                    pt.Add(new XElement(GpxNs + "extensions", extensions));
            }

            trkpts.Add(pt);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(GpxNs + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "gpx-analyzer"),
                new XElement(GpxNs + "trk",
                    new XElement(GpxNs + "name", trackName),
                    new XElement(GpxNs + "trkseg", trkpts))));

        return doc;
    }

    private static List<XElement>? BuildEnrichedExtensions(double speed, double cumDist, double grade, TrackPoint tp)
    {
        var elements = new List<XElement>();

        // gpxa:TrackPointMetrics
        var metrics = new XElement(GpxaNs + "TrackPointMetrics",
            new XElement(GpxaNs + "speed", speed.ToString(CultureInfo.InvariantCulture)),
            new XElement(GpxaNs + "dist", cumDist.ToString(CultureInfo.InvariantCulture)),
            new XElement(GpxaNs + "grade", grade.ToString(CultureInfo.InvariantCulture)));
        elements.Add(metrics);

        // Garmin biometrics
        bool hasBiometrics = tp.HeartRate != null || tp.Cadence != null || tp.Temperature != null;
        if (hasBiometrics)
        {
            var tpe = new XElement(GpxtpxNs + "TrackPointExtension");
            if (tp.HeartRate != null) tpe.Add(new XElement(GpxtpxNs + "hr", tp.HeartRate.Value));
            if (tp.Cadence != null) tpe.Add(new XElement(GpxtpxNs + "cad", tp.Cadence.Value));
            if (tp.Temperature != null) tpe.Add(new XElement(GpxtpxNs + "atemp",
                tp.Temperature.Value.ToString(CultureInfo.InvariantCulture)));
            elements.Add(tpe);
        }

        // Power (bare element)
        if (tp.Power != null)
            elements.Add(new XElement("power", tp.Power.Value));

        return elements.Count > 0 ? elements : null;
    }
}
