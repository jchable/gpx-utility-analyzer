namespace GpxAnalyzer.Api.Services;

using System.IO.Compression;
using GpxAnalyzer.Api.Services.Storage;

public class GpxStorageService
{
    private readonly IStorageService _storage;

    public GpxStorageService(IStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>Store an uploaded GPX file. Returns the storage key.</summary>
    public async Task<string> StoreAsync(Stream gpxStream, string originalFilename, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var safeFilename = Path.GetFileNameWithoutExtension(originalFilename);
        var key = $"{id}_{safeFilename}.gpx";
        await _storage.StoreAsync(gpxStream, key, ct);
        return key;
    }

    /// <summary>
    /// Get a local file lease for the given storage key.
    /// For local storage: returns the direct path (dispose is a no-op).
    /// For S3: downloads to a temp directory; caller must dispose to clean up.
    /// </summary>
    public Task<LocalFileLease> GetLocalPathAsync(string key, CancellationToken ct = default)
        => _storage.EnsureLocalAsync(key, ct);

    /// <summary>Open a read stream for the given storage key.</summary>
    public Task<Stream> GetStreamAsync(string key, CancellationToken ct = default)
        => _storage.GetAsync(key, ct);

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => _storage.ExistsAsync(key, ct);

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => _storage.DeleteAsync(key, ct);

    /// <summary>Delete the GPX file and its original zip archive.</summary>
    public async Task DeleteWithOriginalAsync(string key, CancellationToken ct = default)
    {
        await _storage.DeleteAsync(key, ct);
        await _storage.DeleteAsync(GetArchiveKey(key), ct);
    }

    /// <summary>
    /// Archive the original GPX as a zip. Skipped if archive already exists.
    /// Deletes the original GPX from storage after archiving.
    /// </summary>
    public async Task ArchiveOriginalAsZipAsync(string key, CancellationToken ct = default)
    {
        var archiveKey = GetArchiveKey(key);
        if (await _storage.ExistsAsync(archiveKey, ct))
            return; // already archived from a previous run

        var tempDir = Path.Combine(Path.GetTempPath(), $"gpx-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Download the original GPX to a temp file
            var tempGpxPath = Path.Combine(tempDir, Path.GetFileName(key));
            await using (var src = await _storage.GetAsync(key, ct))
            await using (var dest = File.Create(tempGpxPath))
                await src.CopyToAsync(dest, ct);

            // Create zip in the temp dir
            var zipPath = Path.Combine(tempDir, Path.GetFileName(archiveKey));
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(tempGpxPath, Path.GetFileName(key), CompressionLevel.Optimal);

            // Upload zip to storage
            await using var zipStream = File.OpenRead(zipPath);
            await _storage.StoreAsync(zipStream, archiveKey, ct);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }

        // Delete the original GPX from storage
        await _storage.DeleteAsync(key, ct);
    }

    /// <summary>
    /// Extract the original GPX from the zip archive to a temp directory.
    /// Caller must dispose the returned lease to clean up the temp directory.
    /// </summary>
    public async Task<LocalFileLease> ExtractOriginalToTempAsync(string key, CancellationToken ct = default)
    {
        var archiveKey = GetArchiveKey(key);
        var tempDir = Path.Combine(Path.GetTempPath(), $"gpx-extract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Download the zip archive
        var zipPath = Path.Combine(tempDir, Path.GetFileName(archiveKey));
        await using (var src = await _storage.GetAsync(archiveKey, ct))
        await using (var dest = File.Create(zipPath))
            await src.CopyToAsync(dest, ct);

        // Extract the GPX from the zip
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.FirstOrDefault()
            ?? throw new InvalidOperationException($"No entries in archive: {archiveKey}");
        var extractedPath = Path.Combine(tempDir, entry.FullName);
        entry.ExtractToFile(extractedPath);

        return new LocalFileLease(extractedPath, tempDir);
    }

    /// <summary>Check whether the original zip archive exists for a given key.</summary>
    public Task<bool> HasOriginalArchiveAsync(string key, CancellationToken ct = default)
        => _storage.ExistsAsync(GetArchiveKey(key), ct);

    /// <summary>Replace the GPX at key with a locally processed file.</summary>
    public async Task ReplaceWithProcessedAsync(string key, string localProcessedPath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(localProcessedPath);
        await _storage.StoreAsync(stream, key, ct);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string GetArchiveKey(string key)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(key);
        return $"{nameWithoutExt}_original.zip";
    }
}
