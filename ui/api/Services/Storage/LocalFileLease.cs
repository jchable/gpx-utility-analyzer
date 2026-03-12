namespace GpxAnalyzer.Api.Services.Storage;

/// <summary>
/// A local filesystem path that may own a temporary directory.
/// Disposing cleans up the temp directory (if any).
/// For local storage the lease is a no-op on dispose (path is permanent).
/// </summary>
public sealed class LocalFileLease : IDisposable
{
    public string Path { get; }
    private readonly string? _tempDirectory;

    public LocalFileLease(string path, string? tempDirectory = null)
    {
        Path = path;
        _tempDirectory = tempDirectory;
    }

    public void Dispose()
    {
        if (_tempDirectory is not null && Directory.Exists(_tempDirectory))
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { /* best-effort */ }
    }
}
