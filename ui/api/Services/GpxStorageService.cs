namespace GpxAnalyzer.Api.Services;

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
}
