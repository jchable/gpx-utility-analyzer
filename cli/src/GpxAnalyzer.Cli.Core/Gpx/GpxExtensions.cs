using System.Xml.Linq;

namespace GpxAnalyzer.Cli.Core.Gpx;

/// <summary>
/// Holds biometric data extracted from GPX extensions.
/// </summary>
public readonly record struct PointExtensions(
    int? HeartRate,
    int? Cadence,
    int? Power,
    double? Temperature,
    double? DeviceSpeed = null,
    double? WaterTemp = null);

/// <summary>
/// Parses GPX extensions (Garmin TrackPointExtension v1/v2 and bare power elements).
/// </summary>
public static class GpxExtensionParser
{
    private static readonly XNamespace GarminV1 = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
    private static readonly XNamespace GarminV2 = "http://www.garmin.com/xmlschemas/TrackPointExtension/v2";

    public static PointExtensions Parse(string? innerXml)
    {
        if (string.IsNullOrWhiteSpace(innerXml))
            return default;

        // Wrap in a root element with namespace declarations
        var wrapped = $"""
            <root xmlns:gpxtpx="{GarminV2}"
                  xmlns:ns3="{GarminV1}">
            {innerXml}
            </root>
            """;

        XElement root;
        try
        {
            root = XElement.Parse(wrapped);
        }
        catch
        {
            return default;
        }

        int? heartRate = null;
        int? cadence = null;
        int? power = null;
        double? temperature = null;
        double? deviceSpeed = null;
        double? waterTemp = null;

        // Look for <power> element (may carry parent's default GPX namespace)
        var powerElem = root.Elements().FirstOrDefault(e => e.Name.LocalName == "power");
        if (powerElem != null && int.TryParse(powerElem.Value, out var pw))
            power = pw;

        // Look for TrackPointExtension in both v1 and v2 namespaces
        foreach (var tpe in root.Descendants()
            .Where(e => e.Name.LocalName == "TrackPointExtension"))
        {
            var ns = tpe.Name.Namespace;

            if (heartRate == null)
            {
                var hr = tpe.Element(ns + "hr");
                if (hr != null && int.TryParse(hr.Value, out var hrVal))
                    heartRate = hrVal;
            }

            if (cadence == null)
            {
                var cad = tpe.Element(ns + "cad");
                if (cad != null && int.TryParse(cad.Value, out var cadVal))
                    cadence = cadVal;
            }

            if (temperature == null)
            {
                var temp = tpe.Element(ns + "atemp");
                if (temp != null && double.TryParse(temp.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var tempVal))
                    temperature = tempVal;
            }

            if (deviceSpeed == null)
            {
                var spd = tpe.Element(ns + "speed");
                if (spd != null && double.TryParse(spd.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var spdVal))
                    deviceSpeed = spdVal;
            }

            if (waterTemp == null)
            {
                var wtemp = tpe.Element(ns + "wtemp");
                if (wtemp != null && double.TryParse(wtemp.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var wtempVal))
                    waterTemp = wtempVal;
            }
        }

        return new PointExtensions(heartRate, cadence, power, temperature, deviceSpeed, waterTemp);
    }
}
