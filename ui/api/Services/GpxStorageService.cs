namespace GpxAnalyzer.Api.Services;

using System.IO.Compression;

public class GpxStorageService
{
    private readonly string _basePath;

    public GpxStorageService(IConfiguration configuration)
    {
        _basePath = configuration["Storage:GpxDirectory"] ?? "data/gpx";
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(Stream gpxStream, string originalFilename, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var safeFilename = Path.GetFileNameWithoutExtension(originalFilename);
        var relativePath = $"{id}_{safeFilename}.gpx";
        var fullPath = Path.Combine(_basePath, relativePath);

        using var fileStream = File.Create(fullPath);
        await gpxStream.CopyToAsync(fileStream, ct);

        return relativePath;
    }

    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_basePath, relativePath);
    }

    public bool Exists(string relativePath)
    {
        return File.Exists(Path.Combine(_basePath, relativePath));
    }

    public void Delete(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// Delete both the processed GPX and the original zip archive.
    /// </summary>
    public void DeleteWithOriginal(string relativePath)
    {
        Delete(relativePath);
        var zipPath = GetOriginalZipPath(relativePath);
        if (File.Exists(zipPath))
            File.Delete(zipPath);
    }

    /// <summary>
    /// Derive the full path to the original zip archive from a GPX relative path.
    /// e.g. "abc_trail.gpx" → "{basePath}/abc_trail_original.zip"
    /// </summary>
    public string GetOriginalZipPath(string relativePath)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
        return Path.Combine(_basePath, $"{nameWithoutExt}_original.zip");
    }

    /// <summary>
    /// Archive the original GPX file as a zip and delete the unzipped original.
    /// Skipped if the zip already exists (from a previous processing run).
    /// </summary>
    public void ArchiveOriginalAsZip(string relativePath)
    {
        var gpxFullPath = GetFullPath(relativePath);
        var zipPath = GetOriginalZipPath(relativePath);

        if (File.Exists(zipPath))
            return; // already archived from a previous run

        if (!File.Exists(gpxFullPath))
            throw new FileNotFoundException($"Original GPX not found: {gpxFullPath}");

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(gpxFullPath, Path.GetFileName(gpxFullPath), CompressionLevel.Optimal);
        }

        File.Delete(gpxFullPath);
    }

    /// <summary>
    /// Extract the original GPX from the zip archive to a temporary file.
    /// Returns the full path to the temp file. Caller must delete when done.
    /// </summary>
    public string ExtractOriginalToTemp(string relativePath)
    {
        var zipPath = GetOriginalZipPath(relativePath);
        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Original archive not found: {zipPath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"gpx-reanalyze-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.FirstOrDefault()
            ?? throw new InvalidOperationException($"No entries in archive: {zipPath}");

        var tempPath = Path.Combine(tempDir, entry.FullName);
        entry.ExtractToFile(tempPath);

        return tempPath;
    }

    /// <summary>
    /// Check if the original zip archive exists for a given GPX path.
    /// </summary>
    public bool HasOriginalArchive(string relativePath)
    {
        return File.Exists(GetOriginalZipPath(relativePath));
    }

    /// <summary>
    /// Replace the GPX file at relativePath with a processed export file.
    /// </summary>
    public void ReplaceWithProcessed(string relativePath, string processedFilePath)
    {
        var targetPath = GetFullPath(relativePath);
        File.Copy(processedFilePath, targetPath, overwrite: true);
    }
}
