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

    // Biometrics (null = not present in source GPX)
    public int? HeartRate { get; set; }
    public int? Cadence { get; set; }
    public int? Power { get; set; }
    public double? Temperature { get; set; }

    // GPS quality (null = not present in source GPX)
    public string? Fix { get; set; }        // "2d", "3d", "dgps", ...
    public int? Satellites { get; set; }    // number of satellites used
    public double? Hdop { get; set; }       // horizontal dilution of precision
    public double? Vdop { get; set; }       // vertical dilution of precision
    public double? Pdop { get; set; }       // position dilution of precision

    // Additional extension data (null = not present in source GPX)
    public double? DeviceSpeed { get; set; } // device-reported speed (gpxtpx:speed, m/s)
    public double? WaterTemp { get; set; }   // water temperature (gpxtpx:wtemp, °C)

    /// <summary>Shallow copy. TrackPoint is mutable and is aliased across pipeline
    /// stages and split boundaries; use this wherever a stage must not observe
    /// another stage's in-place mutations.</summary>
    public TrackPoint Clone() => (TrackPoint)MemberwiseClone();
}
