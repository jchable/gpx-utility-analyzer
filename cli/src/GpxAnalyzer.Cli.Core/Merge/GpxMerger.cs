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
            allPoints.Sort((a, b) => a.Time.CompareTo(b.Time));

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
