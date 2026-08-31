using System.Buffers.Binary;

namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Builds the on-disk scenery the DEM-related default tests need: synthetic SRTM tiles,
/// a sandboxed "platform cache directory", and a cache path that makes every download
/// attempt fail instantly without touching the network.
///
/// Everything here is filesystem-only on purpose. The DEM defaults can only be pinned by
/// observing what the CLI does with tiles, and a test that reached the real SRTM mirror
/// would be neither offline nor deterministic.
/// </summary>
internal static class DemFixture
{
    /// <summary>
    /// small.gpx spans 48.8566..48.8640 N / 2.3522..2.3600 E, comfortably inside one
    /// SRTM cell and far from its edges, so exactly one tile is ever requested.
    /// </summary>
    internal const string TileKey = "N48E002";

    /// <summary>The cache layout shards on the first three characters of the tile key.</summary>
    internal const string TilePrefix = "N48";

    /// <summary>Elevation, in metres, baked into the synthetic tiles. Nothing like the GPS data.</summary>
    internal const short TileElevation = 100;

    private const int Grid = 1201;          // SRTM3
    private const short Void = -32768;      // HgtTile.VoidValue

    /// <summary>
    /// Writes a tile whose every sample is <see cref="TileElevation"/>. Applying it moves
    /// every reported elevation to a value that cannot be confused with the GPS track.
    /// </summary>
    internal static void WriteValidTile(string path) => Write(path, static _ => TileElevation);

    /// <summary>
    /// Writes a tile that is void everywhere except a patch covering small.gpx's footprint
    /// (rows 150-200, columns 400-460).
    ///
    /// DemSource.ValidateTile only probes ~100 cells, spaced total/100 apart; because
    /// 1201*1201/100 advances 12 rows and 12 columns at a time, every probe lands near the
    /// grid diagonal and misses that patch. So this tile FAILS validation, yet still returns
    /// real elevations once validation is skipped - which is exactly the difference
    /// --dem-skip-validation controls.
    /// </summary>
    internal static void WriteTileThatFailsValidation(string path) => Write(path, static i =>
    {
        int row = i / Grid, col = i % Grid;
        return row is >= 150 and <= 200 && col is >= 400 and <= 460 ? TileElevation : Void;
    });

    private static void Write(string path, Func<int, short> sample)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new byte[Grid * Grid * 2];
        for (int i = 0; i < Grid * Grid; i++)
            BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(i * 2, 2), sample(i));
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// A --dem-cache value under which any download attempt dies before a socket is opened:
    /// TileDownloader.DownloadTileAsync starts with Directory.CreateDirectory on the tile's
    /// shard folder, and here that path is already occupied by a regular file, so it throws
    /// immediately. This is what lets a test observe the --dem-auto-download default without
    /// the network - the failure is structural, not a timeout.
    /// </summary>
    internal static string CreateDownloadBlockingCache(string workDir, string name = "blocked-cache")
    {
        string cache = Path.Combine(workDir, name);
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, TilePrefix), "occupies the shard directory name");
        return cache;
    }

    /// <summary>
    /// Environment for the child CLI that (a) redirects DemSource.DefaultCacheDir() into the
    /// throwaway working directory so the developer's real SRTM cache cannot leak into a
    /// result, and (b) points every proxy variable at a refused loopback port as a second
    /// line of defence should a download ever get as far as HTTP.
    /// </summary>
    internal static Dictionary<string, string> OfflineEnvironment(string home) => new()
    {
        ["LOCALAPPDATA"] = home,        // DefaultCacheDir() on Windows
        ["XDG_DATA_HOME"] = home,       // ... and via GetFolderPath(LocalApplicationData) on Unix
        ["HOME"] = home,
        ["HTTP_PROXY"] = "http://127.0.0.1:1",
        ["HTTPS_PROXY"] = "http://127.0.0.1:1",
        ["ALL_PROXY"] = "http://127.0.0.1:1",
        ["NO_PROXY"] = "",
    };

    /// <summary>
    /// The tile paths DemSource.DefaultCacheDir() resolves to when the environment from
    /// <see cref="OfflineEnvironment"/> is in force. Both the Windows/XDG_DATA_HOME layout
    /// and the $HOME/.local/share fallback are returned so the fixture works either way.
    /// </summary>
    internal static IEnumerable<string> DefaultCacheTilePaths(string home)
    {
        yield return Path.Combine(home, "gpx-utility-analyzer", "srtm", TilePrefix, TileKey + ".hgt");
        yield return Path.Combine(home, ".local", "share", "gpx-utility-analyzer", "srtm",
            TilePrefix, TileKey + ".hgt");
    }

    /// <summary>The warning DemSource prints when it has a source but no usable tile.</summary>
    internal const string MissingTileWarning = "Warning: DEM tile N48E002 not available, using GPS elevation";

    /// <summary>The sandboxed home directory inside a run's throwaway working directory.</summary>
    internal static string HomeIn(string workDir) => Path.Combine(workDir, "home");

    /// <summary>
    /// Options for any run whose DEM behaviour matters: the platform cache directory is
    /// redirected inside the throwaway working directory, so the developer's own SRTM cache
    /// cannot change a result, and the proxy variables keep a stray download off the network.
    /// </summary>
    internal static CliOptions Offline(Action<string>? arrange = null, Action<string>? inspect = null) => new()
    {
        Arrange = w =>
        {
            Directory.CreateDirectory(HomeIn(w));
            arrange?.Invoke(w);
        },
        Inspect = inspect,
        Environment = w => OfflineEnvironment(HomeIn(w)),
    };
}
