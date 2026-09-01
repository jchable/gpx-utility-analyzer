using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Merge;

namespace GpxAnalyzer.Cli.Tests.Merge;

public class GpxMergerTests
{
    private static GpxDocument DocOf(params TrackPoint[] points) => new()
    {
        Version = "1.1",
        Creator = "test",
        Tracks = [new GpxTrack { Name = "t", Segments = [new GpxSegment { Points = [.. points] }] }],
    };

    [Fact]
    public void Merge_UntimedCourse_PreservesPointOrder()
    {
        // A Komoot / Garmin course export: no <time> on any trkpt, so every
        // point parses to DateTime.MinValue — one block of equal sort keys.
        var course = new List<TrackPoint>();
        for (int i = 1; i <= 20; i++)
            course.Add(new TrackPoint { Lat = i, Lon = 2.0, Ele = 100 });

        var timed = new TrackPoint
        {
            Lat = 100, Lon = 2.0, Ele = 100,
            Time = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime(),
        };

        var merged = GpxMerger.Merge([DocOf([.. course]), DocOf(timed)], sortByTime: true);
        var lats = merged.AllPoints().Select(p => p.Lat).ToList();

        // Untimed points keep their input order; the timed point sorts after them.
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (double)i).Append(100).ToList(), lats);
    }

    [Fact]
    public void Merge_PointsSharingOneSecond_KeepTheirRecordedOrder()
    {
        var t = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var a = new List<TrackPoint>();
        for (int i = 0; i < 10; i++)
            a.Add(new TrackPoint { Lat = 48.0 + i * 0.0001, Lon = 2.0, Time = t });

        var merged = GpxMerger.Merge([DocOf([.. a])], sortByTime: true);
        var lats = merged.AllPoints().Select(p => p.Lat).ToList();

        Assert.Equal(a.Select(p => p.Lat).ToList(), lats);
    }

    [Fact]
    public void Merge_TimedTracks_StillInterleavesByTime()
    {
        var t = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime();
        var morning = DocOf(
            new TrackPoint { Lat = 1, Lon = 2.0, Time = t },
            new TrackPoint { Lat = 3, Lon = 2.0, Time = t.AddMinutes(20) });
        var midday = DocOf(
            new TrackPoint { Lat = 2, Lon = 2.0, Time = t.AddMinutes(10) },
            new TrackPoint { Lat = 4, Lon = 2.0, Time = t.AddMinutes(30) });

        var merged = GpxMerger.Merge([morning, midday], sortByTime: true);

        Assert.Equal([1d, 2d, 3d, 4d], merged.AllPoints().Select(p => p.Lat).ToList());
    }
}
