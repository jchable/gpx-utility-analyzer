namespace GpxAnalyzer.Cli.Core.Gpx;

/// <summary>
/// Represents a parsed GPX document.
/// </summary>
public sealed class GpxDocument
{
    public string Version { get; init; } = "";
    public string Creator { get; init; } = "";
    public List<GpxTrack> Tracks { get; init; } = [];

    /// <summary>
    /// Flattens all tracks/segments into a single list of TrackPoints.
    /// </summary>
    public List<TrackPoint> AllPoints()
    {
        var points = new List<TrackPoint>();
        foreach (var track in Tracks)
            foreach (var segment in track.Segments)
            {
                if (segment.Points.Count > 0)
                    segment.Points[0].StartsNewSegment = points.Count > 0;
                points.AddRange(segment.Points);
            }
        return points;
    }

    public int SegmentCount()
    {
        int count = 0;
        foreach (var track in Tracks)
            count += track.Segments.Count;
        return count;
    }

    public int PointCount()
    {
        int count = 0;
        foreach (var track in Tracks)
            foreach (var segment in track.Segments)
                count += segment.Points.Count;
        return count;
    }
}

public sealed class GpxTrack
{
    public string Name { get; init; } = "";
    public string Desc { get; init; } = "";
    public string? Type { get; init; }
    public List<GpxSegment> Segments { get; init; } = [];
}

public sealed class GpxSegment
{
    public List<TrackPoint> Points { get; init; } = [];
}
