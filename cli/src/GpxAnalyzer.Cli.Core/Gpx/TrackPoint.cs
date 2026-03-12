namespace GpxAnalyzer.Cli.Core.Gpx;

/// <summary>
/// Enriched trackpoint used for internal computation.
/// Mutable class — algorithms modify Ele, CalcSpeed, DistFromPrev, Lat, Lon in place.
/// </summary>
public class TrackPoint
{
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double Ele { get; set; }
    public DateTime Time { get; set; }
    public double Speed { get; set; }      // from GPX (m/s)
    public double CalcSpeed { get; set; }  // computed from distance/time
    public double DistFromPrev { get; set; } // meters from previous point

    // GPS quality (null = not present in source GPX)
    public string? Fix { get; set; }        // "none", "2d", "3d", "dgps", "pps"
    public int? Satellites { get; set; }     // number of satellites
    public double? Hdop { get; set; }        // horizontal dilution of precision
    public double? Vdop { get; set; }        // vertical dilution of precision
    public double? Pdop { get; set; }        // position dilution of precision

    // Device-reported speed (from Garmin gpxtpx:speed extension, m/s)
    public double? DeviceSpeed { get; set; }

    // Biometrics (null = not present in source GPX)
    public int? HeartRate { get; set; }
    public int? Cadence { get; set; }
    public int? Power { get; set; }
    public double? Temperature { get; set; }
    public double? WaterTemp { get; set; }   // °C, from gpxtpx:wtemp
}
