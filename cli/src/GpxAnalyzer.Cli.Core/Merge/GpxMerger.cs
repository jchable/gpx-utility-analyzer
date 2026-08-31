using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Merge;

public static class GpxMerger
{
    public static GpxDocument Merge(List<GpxDocument> docs, bool sortByTime)
    {
        var allPoints = new List<TrackPoint>();
        foreach (var doc in docs)
            allPoints.AddRange(doc.AllPoints());

        if (sortByTime)
            // List<T>.Sort is an introsort and is explicitly unstable. Untimed
            // trkpts all parse to DateTime.MinValue, so a course/route GPX is one
            // block of equal keys and introsort shuffles its geometry into a
            // zig-zag. OrderBy is documented stable.
            allPoints = [.. allPoints.OrderBy(p => p.Time)];

        return new GpxDocument
        {
            Version = "1.1",
            Creator = "gpx-analyzer",
            Tracks =
            [
                new GpxTrack
                {
                    Name = "Merged",
                    Segments = [new GpxSegment { Points = allPoints }]
                }
            ]
        };
    }
}
