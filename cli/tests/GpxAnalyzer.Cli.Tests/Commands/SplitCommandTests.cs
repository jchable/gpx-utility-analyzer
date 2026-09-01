using GpxAnalyzer.Cli.Commands;
using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Tests.Characterization;

namespace GpxAnalyzer.Cli.Tests.Commands;

public class SplitCommandTests
{
    // ------------------------------------------------------------------ #108

    [Theory]
    [InlineData("24h", 24 * 60)]
    [InlineData("90m", 90)]
    [InlineData("30s", 0.5)]
    [InlineData("1.5h", 90)]
    public void ParseDuration_WithUnitSuffix_ParsesAsExpected(string input, double expectedMinutes)
    {
        Assert.Equal(expectedMinutes, SplitCommand.ParseDuration(input).TotalMinutes, 6);
    }

    [Theory]
    [InlineData("24")]      // user meant 24 hours; TimeSpan.TryParse reads 24 DAYS
    [InlineData("1")]
    [InlineData("")]
    [InlineData("banana")]
    public void ParseDuration_WithoutAUnit_IsRejected(string input)
    {
        Assert.Equal(TimeSpan.Zero, SplitCommand.ParseDuration(input));
    }

    /// <summary>
    /// End-to-end proof of #108: a bare "24" used to be accepted as 24 DAYS, so a
    /// multi-day track came out as a single segment identical to the input and the
    /// command still reported success.
    /// </summary>
    [Fact]
    public void Split_UnitLessInterval_IsRejectedInsteadOfMeaningDays()
    {
        string[] produced = [];
        var r = CliRunner.Run(
            new CliOptions
            {
                Arrange = WriteMultiDayFixture,
                Inspect = w => produced = Directory.Exists(Path.Combine(w, "out"))
                    ? [.. Directory.GetFiles(Path.Combine(w, "out"))]
                    : [],
            },
            "split", "multiday.gpx", "--interval", "24", "--output-dir", "out",
            "--dem-auto-download", "false");

        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("invalid interval '24'", r.StdErr);
        Assert.Contains("unit suffix", r.StdErr);
        // It must not have quietly split the file into one 24-day bucket.
        Assert.DoesNotContain("Split into", r.StdErr);
        Assert.Empty(produced);
    }

    // ------------------------------------------------------------------- #85

    /// <summary>
    /// End-to-end regression test for #85, driving the real `split` command.
    ///
    /// A boundary point may be duplicated between continuous buckets, but a recording
    /// gap must start the next file at the first point actually recorded after it.
    /// </summary>
    [Fact]
    public void Split_MultiDayTrack_DoesNotLeakSmoothedValuesIntoTheNextSegmentsFile()
    {
        List<TrackPoint> source = [];
        List<List<TrackPoint>> written = [];

        var r = CliRunner.Run(
            new CliOptions
            {
                Arrange = w =>
                {
                    WriteMultiDayFixture(w);
                    source = GpxParser.ParseFile(Path.Combine(w, "multiday.gpx")).AllPoints();
                },
                Inspect = w => written = [.. Directory
                    .GetFiles(Path.Combine(w, "out"), "*.gpx")
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => GpxParser.ParseFile(p).AllPoints())],
            },
            "split", "multiday.gpx", "--interval", "24h", "--output-dir", "out",
            "--dem-auto-download", "false",
            // Both mutating steps on, so a leak is guaranteed to be visible.
            "--smoothing", "medium", "--track-smoothing", "medium");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("Error processing segment", r.StdErr);
        Assert.True(written.Count >= 2, $"expected at least 2 segment files, got {written.Count}");

        for (int i = 0; i + 1 < written.Count; i++)
        {
            var tail = written[i][^1];
            var head = written[i + 1][0];

            Assert.True(head.Time - tail.Time > ElevationSmoother.GapThreshold);
            Assert.Contains(source, p => p.Time == head.Time);
        }
    }

    // --------------------------------------------------------------- fixture

    /// <summary>
    /// A 3-day track with a saw-tooth elevation profile, so smoothing demonstrably
    /// changes every value it touches. 40 points per day at 2-minute spacing - the
    /// spacing matters: ElevationSmoother.GapThreshold is 10 minutes and it smooths
    /// each time-continuous run independently, so a coarser fixture makes every point
    /// its own run and smoothing a silent no-op.
    /// The day-sized gaps put each day in its own 24h bucket.
    /// </summary>
    private static void WriteMultiDayFixture(string workDir)
    {
        var t0 = DateTime.Parse("2024-01-01T08:00:00Z").ToUniversalTime();
        var points = new List<TrackPoint>();
        for (int day = 0; day < 3; day++)
            for (int i = 0; i < 40; i++)
                points.Add(new TrackPoint
                {
                    Lat = 45.0 + i * 0.001,
                    Lon = 6.0 + (i % 3) * 0.0005,
                    Ele = 1000 + (i % 7) * 60,
                    Time = t0.AddDays(day).AddMinutes(i * 2),
                });

        GpxWriter.Write(Path.Combine(workDir, "multiday.gpx"), points, "multiday");
    }
}
