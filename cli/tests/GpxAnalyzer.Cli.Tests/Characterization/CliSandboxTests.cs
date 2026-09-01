using System.Text.Json;

namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// #135. The suite used to be isolated from the developer's own SRTM cache only where a test
/// remembered to opt in through a fixture helper. Isolation is now unconditional, installed by
/// <see cref="CliRunner"/> for every run, so these tests deliberately use NO DEM helper beyond
/// the synthetic tiles themselves: whatever they assert has to hold for a test that never
/// thought about the question.
/// </summary>
public class CliSandboxTests
{
    /// <summary>Max elevation small.gpx reports from its own data, with no DEM correction.</summary>
    private const double GpsMaxElevation = 43.333333333333336;

    /// <summary>
    /// `--dem-dir x --dem-auto-download false` still BUILDS a DEM source, and that source falls
    /// back to DemSource.DefaultCacheDir() because --dem-cache is empty. Unsandboxed, that is
    /// %LOCALAPPDATA%\gpx-utility-analyzer\srtm: on a machine holding N48E002.hgt this exact
    /// run reports 46.26 m instead of small.gpx's own 43.33, and the result depends on what the
    /// developer happens to have downloaded.
    /// </summary>
    [Fact]
    public void ADemSourceWithNoExplicitCache_CannotReachTheRealPlatformCache()
    {
        var r = CliRunner.Run(
            new CliOptions { Arrange = w => Directory.CreateDirectory(Path.Combine(w, "dem")) },
            "-f", "json", "analyze", "--dem-dir", "dem",
            "--dem-auto-download", "false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.Contains(DemFixture.MissingTileWarning, r.StdErr);
        Assert.Equal(GpsMaxElevation, MaxElevation(r.StdOut), 6);
    }

    /// <summary>
    /// And the sandbox is a real redirection rather than an empty-directory accident: the tile
    /// the CLI does find under the platform cache is the one this test put there.
    /// </summary>
    [Fact]
    public void ThePlatformCacheTheCliResolves_IsInsideTheRunsWorkingDirectory()
    {
        var r = CliRunner.Run(
            new CliOptions
            {
                Arrange = w =>
                {
                    Directory.CreateDirectory(Path.Combine(w, "dem"));
                    foreach (var tile in DemFixture.DefaultCacheTilePaths(CliRunner.HomeIn(w)))
                        DemFixture.WriteValidTile(tile);
                },
            },
            "-f", "json", "analyze", "--dem-dir", "dem",
            "--dem-auto-download", "false", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.DoesNotContain("DEM tile", r.StdErr);
        Assert.Equal(DemFixture.TileElevation, MaxElevation(r.StdOut), 6);
    }

    /// <summary>
    /// The default-on auto-download is the only path in the codebase that opens a socket. Left
    /// entirely to its defaults - no --dem-cache, no --dem-dir, nothing blocking it on the
    /// filesystem - it must still come back empty-handed rather than fetching a tile, because
    /// the cache it would write to and the proxy it would dial are both inside the sandbox.
    /// </summary>
    [Fact]
    public void AutoDownloadLeftAtItsDefault_FetchesNothing()
    {
        string[] fetched = [];
        var r = CliRunner.Run(
            new CliOptions
            {
                Inspect = w => fetched = Directory.GetFiles(w, "*.hgt", SearchOption.AllDirectories),
            },
            "-f", "json", "analyze", "small.gpx");

        Assert.Equal(0, r.ExitCode);
        Assert.Empty(fetched);
        Assert.Contains(DemFixture.MissingTileWarning, r.StdErr);
        Assert.Equal(GpsMaxElevation, MaxElevation(r.StdOut), 6);
    }

    private static double MaxElevation(string stdout) =>
        JsonDocument.Parse(stdout).RootElement.GetProperty("max_elevation_m").GetDouble();
}
