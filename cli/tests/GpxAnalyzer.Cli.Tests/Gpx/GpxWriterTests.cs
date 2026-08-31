using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Tests.Gpx;

public class GpxWriterTests
{
    private static List<TrackPoint> SamplePoints()
    {
        var t0 = DateTime.Parse("2024-01-02T10:04:05Z").ToUniversalTime();
        return
        [
            new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0 },
            new() { Lat = 48.001, Lon = 2.0, Ele = 110, Time = t0.AddSeconds(30) },
        ];
    }

    /// <summary>
    /// A bare filename with no directory component, unique per test run. These
    /// tests must NOT change the process working directory: xUnit runs test
    /// classes in parallel and the CWD is process-wide, so mutating it breaks
    /// every sibling test that resolves testdata/ relatively.
    /// </summary>
    private static string BareName(string prefix) => $"{prefix}_{Guid.NewGuid():N}.gpx";

    [Fact]
    public void Write_BareFilename_DoesNotThrow()
    {
        // This is exactly what `merge` does with its default --output merged.gpx.
        var name = BareName("merged");
        try
        {
            GpxWriter.Write(name, SamplePoints(), "merged");
            Assert.True(File.Exists(name));
        }
        finally { File.Delete(name); }
    }

    [Fact]
    public void WriteEnriched_BareFilename_DoesNotThrow()
    {
        var name = BareName("enriched");
        try
        {
            GpxWriter.WriteEnriched(name, SamplePoints(), "enriched");
            Assert.True(File.Exists(name));
        }
        finally { File.Delete(name); }
    }

    [Fact]
    public void Write_PathWithDirectory_StillCreatesIt()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var outPath = Path.Combine(tmp.FullName, "nested", "out.gpx");
            GpxWriter.Write(outPath, SamplePoints(), "out");
            Assert.True(File.Exists(outPath));
        }
        finally { tmp.Delete(recursive: true); }
    }
}
