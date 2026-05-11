namespace GpxAnalyzer.Api.Services.Storage;

public class LocalStorageService : IStorageService
{
    private readonly string _basePath;

    public LocalStorageService(IConfiguration configuration)
    {
        _basePath = configuration["Storage:GpxDirectory"] ?? "data/gpx";
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

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, key);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Path.Combine(_basePath, key)));

    /// <summary>Returns the direct local path — no download needed.</summary>
    public Task<LocalFileLease> EnsureLocalAsync(string key, CancellationToken ct = default)
        => Task.FromResult(new LocalFileLease(Path.Combine(_basePath, key)));

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => throw new NotSupportedException("Presigned URLs are not supported for local storage.");
}
