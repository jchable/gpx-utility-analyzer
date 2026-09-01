namespace GpxAnalyzer.Api.Services.Storage;

public class LocalStorageService : IStorageService
{
    /// <summary>
    /// How long to keep retrying a delete that a concurrent reader is blocking.
    /// The windows where the processing pipeline holds a GPX open (archiving the
    /// original, writing back the processed file) are short.
    /// </summary>
    private const int DeleteAttempts = 6;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly string _basePath;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(IConfiguration configuration, ILogger<LocalStorageService> logger)
    {
        _basePath = configuration["Storage:GpxDirectory"] ?? "data/gpx";
        _logger = logger;
        Directory.CreateDirectory(_basePath);
    }

    public async Task StoreAsync(Stream content, string key, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, key);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Storage object not found: {key}", fullPath);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    /// <summary>
    /// Deletes a stored object, tolerating a handle another thread still holds.
    ///
    /// Deleting an activity must not fail because the background worker happens to
    /// be reading or rewriting its GPX at that instant (#131): the row is what the
    /// user asked to remove. Retry briefly, and if the file is still locked leave it
    /// behind as an unreferenced orphan rather than failing the request.
    /// </summary>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, key);

        for (var attempt = 1; attempt <= DeleteAttempts; attempt++)
        {
            try
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    _logger.LogWarning(ex,
                        "Storage object {Key} is still in use after {Attempts} attempts; " +
                        "leaving it on disk as an orphan", key, DeleteAttempts);
                    return;
                }

                await Task.Delay(DeleteRetryDelay, ct);
            }
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Path.Combine(_basePath, key)));

    /// <summary>Returns the direct local path — no download needed.</summary>
    public Task<LocalFileLease> EnsureLocalAsync(string key, CancellationToken ct = default)
        => Task.FromResult(new LocalFileLease(Path.Combine(_basePath, key)));

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => throw new NotSupportedException("Presigned URLs are not supported for local storage.");
}
