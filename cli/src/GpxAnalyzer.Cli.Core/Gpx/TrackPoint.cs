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

    // ---------------------------------------------------------------- recording boundaries
    //
    // Three bits, three owners. Two of them are DERIVED from the point data and
    // ComputePipeline re-derives them after anomaly correction has rewritten timestamps and
    // positions, so each owning pass must assign its own bit outright - a bit that a pass can
    // only ever set, never clear, survives the data that justified it.

    /// <summary>
    /// The source GPX opened a new &lt;trkseg&gt; at this point. Structural: it describes the
    /// file, not the numbers, and is owned by the GPX layer (<see cref="GpxDocument.AllPoints"/>).
    /// The compute pipeline reads it and never writes it; <see cref="GpxWriter"/> writes it back
    /// out as a &lt;trkseg&gt; break so the boundary survives a write/re-read cycle.
    /// </summary>
    public bool StartsNewSegment { get; set; }

    /// <summary>
    /// The interval from the previous point is longer than
    /// <see cref="Elevation.ElevationSmoother.GapThreshold"/>: the recorder was off, so nothing
    /// happened in between. Derived - recomputed in full by
    /// <see cref="Stats.SpeedCalculator.EnrichPoints"/> on every pass.
    /// </summary>
    public bool AfterRecordingGap { get; set; }

    /// <summary>
    /// The distance and speed from the previous point were discarded as implausible. Derived -
    /// recomputed in full by <see cref="Stats.SpeedCalculator.ClampSpeeds"/> on every pass.
    /// </summary>
    public bool SpeedClamped { get; set; }

    /// <summary>
    /// No time was recorded between the previous point and this one. A speed clamp is
    /// deliberately NOT one of these: an implausible speed discredits the distance between two
    /// fixes, not the seconds that elapsed between them.
    /// </summary>
    public bool BreaksRecordedTime => StartsNewSegment || AfterRecordingGap;

    /// <summary>No measurable path from the previous point, whatever the reason.</summary>
    public bool BreaksPath => BreaksRecordedTime || SpeedClamped;

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
