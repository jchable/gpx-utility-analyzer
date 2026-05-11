namespace GpxAnalyzer.Api.Services.Storage;

/// <summary>
/// Generic object storage abstraction (local filesystem or S3-compatible).
/// </summary>
public interface IStorageService
{
    /// <summary>Store a stream under the given key.</summary>
    Task StoreAsync(Stream content, string key, CancellationToken ct = default);

    /// <summary>Open a read stream for the given key.</summary>
    Task<Stream> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Delete an object. No-op if it doesn't exist.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Check whether an object exists.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Return a <see cref="LocalFileLease"/> whose <c>Path</c> is a local filesystem path
    /// for the given key. For local storage this is the direct path (no temp copy).
    /// For S3 the object is downloaded to a temporary directory; dispose the lease to clean up.
    /// </summary>
    Task<LocalFileLease> EnsureLocalAsync(string key, CancellationToken ct = default);

    /// <summary>Generate a presigned download URL valid for the given duration.</summary>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}
