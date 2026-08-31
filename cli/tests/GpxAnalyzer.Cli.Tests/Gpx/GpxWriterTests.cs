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

    [Fact]
    public void Write_NonEnriched_PreservesBiometricsAndGpsQuality()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var t0 = DateTime.Parse("2024-01-02T10:04:05Z").ToUniversalTime();
            var points = new List<TrackPoint>
            {
                new()
                {
                    Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0,
                    HeartRate = 140, Cadence = 85, Power = 210, Temperature = 18.5,
                    Satellites = 9, Hdop = 1.1, Fix = "3d",
                },
                new()
                {
                    Lat = 48.001, Lon = 2.0, Ele = 110, Time = t0.AddSeconds(30),
                    HeartRate = 145, Cadence = 86, Power = 215, Temperature = 18.6,
                    Satellites = 9, Hdop = 1.2, Fix = "3d",
                },
            };

            var outPath = Path.Combine(tmp.FullName, "out.gpx");
            // This is the writer `split` and `merge` use; neither has --enrich.
            GpxWriter.Write(outPath, points, "out");

            var reparsed = GpxParser.ParseFile(outPath).AllPoints();

            Assert.Equal(140, reparsed[0].HeartRate);
            Assert.Equal(85, reparsed[0].Cadence);
            Assert.Equal(210, reparsed[0].Power);
            Assert.Equal(18.5, reparsed[0].Temperature);
            Assert.Equal(9, reparsed[0].Satellites);
            Assert.Equal(1.1, reparsed[0].Hdop);
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public void WriteEnriched_PowerElement_IsInTheGpxDefaultNamespace()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var t0 = DateTime.Parse("2024-01-02T10:04:05Z").ToUniversalTime();
            var points = new List<TrackPoint>
            {
                new() { Lat = 48.0, Lon = 2.0, Ele = 100, Time = t0, Power = 210 },
                new() { Lat = 48.001, Lon = 2.0, Ele = 110, Time = t0.AddSeconds(30), Power = 215 },
            };
            var outPath = Path.Combine(tmp.FullName, "enriched.gpx");
            GpxWriter.WriteEnriched(outPath, points, "enriched");

            System.Xml.Linq.XNamespace ns = "http://www.topografix.com/GPX/1/1";
            var doc = System.Xml.Linq.XDocument.Load(outPath);
            var ext = doc.Descendants(ns + "extensions").First();

            // This is the exact lookup ui/api ProfileComputationService performs.
            Assert.NotNull(ext.Element(ns + "power"));
            Assert.Equal("210", ext.Element(ns + "power")!.Value);
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("fi-FI")]
    [InlineData("cs-CZ")]
    public void Write_UnderACultureWithANonColonTimeSeparator_EmitsValidIsoTimestamps(string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo(culture);

            var outPath = Path.Combine(tmp.FullName, "out.gpx");
            GpxWriter.Write(outPath, SamplePoints(), "out");

            var xml = File.ReadAllText(outPath);
            Assert.Contains("2024-01-02T10:04:05Z", xml);
            Assert.DoesNotContain("10.04.05", xml);

            // And it must round-trip through the parser.
            var reparsed = GpxParser.ParseFile(outPath).AllPoints();
            Assert.Equal(2, reparsed.Count);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
            tmp.Delete(recursive: true);
        }
    }
}
