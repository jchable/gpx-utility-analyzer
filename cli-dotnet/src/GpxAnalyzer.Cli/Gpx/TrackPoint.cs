namespace GpxAnalyzer.Cli.Gpx;

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
}
