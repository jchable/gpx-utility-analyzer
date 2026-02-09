namespace GpxAnalyzer.Api.Services.Integrations;

using System.Xml.Linq;
using Dynastream.Fit;

public static class FitToGpxConverter
{
    private const double SemicirclesToDegrees = 180.0 / 2147483648.0; // 180 / 2^31

    public static Stream Convert(Stream fitStream)
    {
        var points = new List<GpxPoint>();

        var decoder = new Decode();
        var listener = new MesgBroadcaster();

        listener.RecordMesgEvent += (_, args) =>
        {
            var record = args.mesg;

            var lat = record.GetFieldValue(RecordMesg.FieldDefNum.PositionLat);
            var lon = record.GetFieldValue(RecordMesg.FieldDefNum.PositionLong);

            if (lat is null || lon is null)
                return;

            var point = new GpxPoint
            {
                Lat = System.Convert.ToInt32(lat) * SemicirclesToDegrees,
                Lon = System.Convert.ToInt32(lon) * SemicirclesToDegrees,
            };

            var altitude = record.GetFieldValue(RecordMesg.FieldDefNum.Altitude);
            if (altitude is not null)
                point.Elevation = System.Convert.ToDouble(altitude);

            var timestamp = record.GetFieldValue(RecordMesg.FieldDefNum.Timestamp);
            if (timestamp is Dynastream.Fit.DateTime fitTime)
                point.Time = fitTime.GetDateTime();

            var hr = record.GetFieldValue(RecordMesg.FieldDefNum.HeartRate);
            if (hr is not null)
                point.HeartRate = System.Convert.ToInt32(hr);

            var cadence = record.GetFieldValue(RecordMesg.FieldDefNum.Cadence);
            if (cadence is not null)
                point.Cadence = System.Convert.ToInt32(cadence);

            var power = record.GetFieldValue(RecordMesg.FieldDefNum.Power);
            if (power is not null)
                point.Power = System.Convert.ToInt32(power);

            points.Add(point);
        };

        decoder.MesgEvent += listener.OnMesg;
        decoder.Read(fitStream);

        return BuildGpx(points);
    }

    private static Stream BuildGpx(List<GpxPoint> points)
    {
        XNamespace ns = "http://www.topografix.com/GPX/1/1";
        XNamespace gpxtpx = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

        var trkpts = points.Select(p =>
        {
            var el = new XElement(ns + "trkpt",
                new XAttribute("lat", p.Lat),
                new XAttribute("lon", p.Lon));

            if (p.Elevation.HasValue)
                el.Add(new XElement(ns + "ele", p.Elevation.Value));

            if (p.Time.HasValue)
                el.Add(new XElement(ns + "time", p.Time.Value.ToString("o")));

            if (p.HeartRate.HasValue || p.Cadence.HasValue || p.Power.HasValue)
            {
                var tpx = new XElement(gpxtpx + "TrackPointExtension");
                if (p.HeartRate.HasValue)
                    tpx.Add(new XElement(gpxtpx + "hr", p.HeartRate.Value));
                if (p.Cadence.HasValue)
                    tpx.Add(new XElement(gpxtpx + "cad", p.Cadence.Value));
                if (p.Power.HasValue)
                    tpx.Add(new XElement(gpxtpx + "power", p.Power.Value));

                el.Add(new XElement(ns + "extensions", tpx));
            }

            return el;
        });

        var gpx = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "gpx-analyzer-garmin-import"),
                new XAttribute(XNamespace.Xmlns + "gpxtpx", gpxtpx),
                new XElement(ns + "trk",
                    new XElement(ns + "name", "Garmin Activity"),
                    new XElement(ns + "trkseg", trkpts))));

        var ms = new MemoryStream();
        gpx.Save(ms);
        ms.Position = 0;
        return ms;
    }

    private class GpxPoint
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double? Elevation { get; set; }
        public System.DateTime? Time { get; set; }
        public int? HeartRate { get; set; }
        public int? Cadence { get; set; }
        public int? Power { get; set; }
    }
}
