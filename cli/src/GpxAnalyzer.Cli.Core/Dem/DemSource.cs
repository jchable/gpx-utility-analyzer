using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Dem;

/// <summary>
/// Provides DEM elevation lookups from SRTM HGT files.
/// Implements IElevationProvider and IElevationPreloader.
/// </summary>
public sealed class DemSource : IElevationProvider, IElevationPreloader
{
    private readonly string _dir;
    private readonly string _cacheDir;
    private readonly bool _autoDownload;
    private readonly int _maxMemoryMB;
    private readonly bool _skipValidation;
    private readonly Dictionary<string, HgtTile?> _tiles = new();
    private readonly HashSet<string> _warns = [];

    private DemSource(string dir, string cacheDir, bool autoDownload, int maxMemoryMB, bool skipValidation)
    {
        _dir = dir;
        _cacheDir = cacheDir;
        _autoDownload = autoDownload;
        _maxMemoryMB = maxMemoryMB;
        _skipValidation = skipValidation;
    }

    public static DemSource Create(string dir) => new(dir, "", false, 0, false);

    public static DemSource CreateWithCache(string dir, string cacheDir, bool autoDownload)
        => new(dir, cacheDir, autoDownload, 0, false);

    public static DemSource CreateAuto(string cacheDir)
        => new("", cacheDir, true, 0, false);

    public DemSource WithMaxMemory(int mb) => new(_dir, _cacheDir, _autoDownload, mb, _skipValidation);
    public DemSource WithSkipValidation(bool skip) => new(_dir, _cacheDir, _autoDownload, _maxMemoryMB, skip);

    public static string DefaultCacheDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
                return Path.Combine(localAppData, "gpx-utility-analyzer", "srtm");
        }
        var cacheDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(cacheDir))
            return Path.Combine(cacheDir, "gpx-utility-analyzer", "srtm");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cache", "gpx-utility-analyzer", "srtm");
    }

    public static string TileCachePath(string cacheDir, string key)
    {
        string prefix = key[..3];
        return Path.Combine(cacheDir, prefix, key + ".hgt");
    }

    public (double Elevation, bool Ok) GetElevation(double lat, double lon)
    {
        string key = HgtTile.TileKey(lat, lon);
        if (!_tiles.TryGetValue(key, out var tile))
        {
            tile = LoadTile(key);
            _tiles[key] = tile;
        }
        if (tile == null) return (0, false);

        // SRTM tiles duplicate their shared edge row and column, so every point
        // inside a tile is fully interpolable from that tile alone.
        double row = (tile.GridSize - 1) * (tile.LatOrigin + 1.0 - lat);
        double col = (tile.GridSize - 1) * (lon - tile.LonOrigin);

        if (row < 0 || row > tile.GridSize - 1 || col < 0 || col > tile.GridSize - 1)
            return (0, false);

        return tile.GetElevation(lat, lon);
    }

    public async Task PreloadAsync(List<TrackPoint> points)
    {
        var needed = CollectTileKeys(points);
        if (needed.Count == 0) return;

        await EnsureTilesOnDiskAsync(needed);
        var (tileFiles, totalBytes) = ResolveTileFiles(needed);

        if (_maxMemoryMB > 0)
        {
            long limitBytes = (long)_maxMemoryMB * 1024 * 1024;
            if (totalBytes > limitBytes)
            {
                long totalMB = totalBytes / (1024 * 1024);
                throw new InvalidOperationException(
                    $"DEM tiles require ~{totalMB} MB ({tileFiles.Count} tiles), " +
                    $"but --dem-max-memory is {_maxMemoryMB} MB");
            }
        }

        foreach (var (key, path) in tileFiles)
        {
            if (_tiles.ContainsKey(key)) continue;
            try
            {
                var tile = HgtTile.Load(path);
                _tiles[key] = (_skipValidation || ValidateTile(tile)) ? tile : null;
            }
            catch
            {
                _tiles[key] = null;
            }
        }
    }

    /// <summary>
    /// The tiles the track genuinely needs — one per point. No neighbours: SRTM
    /// tiles duplicate their shared edge row and column, so GetElevation never
    /// reads outside the tile a point falls in.
    /// </summary>
    private static List<string> CollectTileKeys(List<TrackPoint> points)
    {
        var seen = new HashSet<string>();
        foreach (var p in points)
            seen.Add(HgtTile.TileKey(p.Lat, p.Lon));
        return [.. seen];
    }

    private async Task EnsureTilesOnDiskAsync(List<string> keys)
    {
        if (!_autoDownload || string.IsNullOrEmpty(_cacheDir)) return;

        var toDownload = keys.Where(k => !TileExistsOnDisk(k)).ToList();
        if (toDownload.Count == 0) return;

        using var semaphore = new SemaphoreSlim(4);
        var tasks = toDownload.Select(async key =>
        {
            await semaphore.WaitAsync();
            try
            {
                string dest = TileCachePath(_cacheDir, key);
                await TileDownloader.DownloadTileAsync(key, dest);
            }
            catch
            {
                // Download failure is non-fatal; tile will be null
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    private bool TileExistsOnDisk(string key)
    {
        if (!string.IsNullOrEmpty(_dir) && File.Exists(Path.Combine(_dir, key + ".hgt")))
            return true;
        if (!string.IsNullOrEmpty(_cacheDir))
        {
            if (File.Exists(TileCachePath(_cacheDir, key))) return true;
            if (File.Exists(Path.Combine(_cacheDir, key + ".hgt"))) return true;
        }
        return false;
    }

    private (Dictionary<string, string> Files, long TotalBytes) ResolveTileFiles(List<string> keys)
    {
        var files = new Dictionary<string, string>();
        long total = 0;
        foreach (var key in keys)
        {
            string? path = FindTilePath(key);
            if (path == null) continue;
            var info = new FileInfo(path);
            if (!info.Exists) continue;
            files[key] = path;
            total += info.Length;
        }
        return (files, total);
    }

    private string? FindTilePath(string key)
    {
        if (!string.IsNullOrEmpty(_dir))
        {
            string p = Path.Combine(_dir, key + ".hgt");
            if (File.Exists(p)) return p;
        }
        if (!string.IsNullOrEmpty(_cacheDir))
        {
            string hi = TileCachePath(_cacheDir, key);
            if (File.Exists(hi)) return hi;
            string flat = Path.Combine(_cacheDir, key + ".hgt");
            if (File.Exists(flat)) return flat;
        }
        return null;
    }

    private HgtTile? LoadTile(string key)
    {
        // Try user directory
        if (!string.IsNullOrEmpty(_dir))
        {
            var tile = TryLoad(Path.Combine(_dir, key + ".hgt"));
            if (tile != null) return tile;
        }

        // Try cache (hierarchical, then flat)
        if (!string.IsNullOrEmpty(_cacheDir))
        {
            var tile = TryLoad(TileCachePath(_cacheDir, key));
            if (tile != null) return tile;
            tile = TryLoad(Path.Combine(_cacheDir, key + ".hgt"));
            if (tile != null) return tile;

            // Auto-download
            if (_autoDownload)
            {
                string dest = TileCachePath(_cacheDir, key);
                try
                {
                    TileDownloader.DownloadTileAsync(key, dest).GetAwaiter().GetResult();
                    tile = TryLoad(dest);
                    if (tile != null) return tile;
                }
                catch { }
            }
        }

        if (_warns.Add(key))
            Console.Error.WriteLine($"Warning: DEM tile {key} not available, using GPS elevation");
        return null;
    }

    private HgtTile? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var tile = HgtTile.Load(path);
            return (_skipValidation || ValidateTile(tile)) ? tile : null;
        }
        catch { return null; }
    }

    public static bool ValidateTile(HgtTile tile)
    {
        int total = tile.GridSize * tile.GridSize;
        int step = total / 100;
        if (step < 1) step = 1;
        for (int i = 0; i < total; i += step)
        {
            if (tile.Data[i] != HgtTile.VoidValue)
                return true;
        }
        return false;
    }
}

public interface IElevationProvider
{
    (double Elevation, bool Ok) GetElevation(double lat, double lon);
}

public interface IElevationPreloader
{
    Task PreloadAsync(List<TrackPoint> points);
}
