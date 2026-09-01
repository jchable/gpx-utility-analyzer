using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Tests.Characterization;

namespace GpxAnalyzer.Cli.Tests.Dem;

/// <summary>
/// Offline DEM tests: every tile is synthesised on disk and auto-download is off,
/// so nothing here touches the network.
/// </summary>
public class DemSourceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "gpxa-dem-tests-" + Guid.NewGuid().ToString("N"));

    public DemSourceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private void WriteTile(string key) =>
        DemFixture.WriteValidTile(Path.Combine(_dir, key + ".hgt"));

    /// <summary>
    /// A point ~55 m north of the tile's south edge and ~37 m west of its east edge,
    /// i.e. inside the ~92 m band CollectTileKeys used to treat as "needs neighbours".
    /// </summary>
    private static List<TrackPoint> PointOnTileCorner() =>
    [
        new() { Lat = 48.0005, Lon = 2.9995, Time = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
    ];

    // #106 — CollectTileKeys pulled in the south, east and south-east neighbours for
    // any point near a tile edge. They only ever fed CrossTileElevation, which is
    // unreachable, but PreloadAsync still loaded them and they counted toward
    // --dem-max-memory.
    [Fact]
    public async Task PreloadAsync_PointNearATileCorner_DoesNotLoadTheNeighbourTiles()
    {
        WriteTile("N48E002");   // the tile the point is actually in
        WriteTile("N47E002");   // south neighbour
        WriteTile("N48E003");   // east neighbour
        WriteTile("N47E003");   // south-east neighbour

        // One SRTM3 tile is ~2.75 MiB; four are ~11 MiB. A 6 MB budget is ample for
        // the tile that is genuinely needed and impossible for the phantom set.
        var dem = DemSource.Create(_dir).WithMaxMemory(6);

        var ex = await Record.ExceptionAsync(() => dem.PreloadAsync(PointOnTileCorner()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PreloadAsync_PointNearATileCorner_StillResolvesTheElevation()
    {
        WriteTile("N48E002");
        WriteTile("N47E002");
        WriteTile("N48E003");
        WriteTile("N47E003");

        var dem = DemSource.Create(_dir);
        await dem.PreloadAsync(PointOnTileCorner());

        var (elevation, ok) = dem.GetElevation(48.0005, 2.9995);

        Assert.True(ok);
        Assert.Equal(DemFixture.TileElevation, elevation, 6);
    }

    [Fact]
    public async Task PreloadAsync_MemoryBudgetTooSmallForTheRealTiles_StillAborts()
    {
        WriteTile("N48E002");

        // 1 MB cannot hold the ~2.75 MiB tile the point genuinely needs, so the
        // budget check must still fire — the fix must not disable it.
        var dem = DemSource.Create(_dir).WithMaxMemory(1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dem.PreloadAsync(PointOnTileCorner()));

        Assert.Contains("--dem-max-memory", ex.Message);
    }
}
