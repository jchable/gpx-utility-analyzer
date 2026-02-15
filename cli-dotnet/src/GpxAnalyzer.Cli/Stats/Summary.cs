namespace GpxAnalyzer.Cli.Stats;

/// <summary>
/// Holds all computed statistics for a GPX file or segment.
/// </summary>
public sealed class Summary
{
    // Distance
    public double TotalDistance { get; set; }   // meters (2D)
    public double TotalDistance3D { get; set; } // meters (3D)

    // Elevation
    public ElevationResult Elevation { get; set; } = new();

    // Time
    public TimeSpan TotalTime { get; set; }
    public TimeSpan MovingTime { get; set; }
    public TimeSpan StoppedTime { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // Speed & Pace
    public SpeedResult Speed { get; set; } = new();

    // Points metadata
    public int PointCount { get; set; }
    public int FilteredPoints { get; set; }
    public int SegmentCount { get; set; }
    public double PointsPerKm { get; set; }

    // Stops
    public List<Stop> Stops { get; set; } = [];
    public int StopCount { get; set; }
    public TimeSpan TotalStopTime { get; set; }
    public Stop? LongestStop { get; set; }
    public TimeSpan AvgStopDuration { get; set; }

    // Biometrics
    public BiometricsResult Biometrics { get; set; } = new();
}
