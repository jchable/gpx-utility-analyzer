namespace GpxAnalyzer.Api.Services.Storage;

using Amazon.S3;
using Amazon.S3.Model;

public class S3StorageService : IStorageService, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly string _basePrefix;

    public S3StorageService(IConfiguration configuration)
    {
        var s3 = configuration.GetSection("Storage:S3");
        var endpoint = s3["Endpoint"] ?? "http://localhost:9000";
        var accessKey = s3["AccessKey"] ?? "minioadmin";
        var secretKey = s3["SecretKey"] ?? "minioadmin";
        _bucketName = s3["BucketName"] ?? "gpx-files";
        _basePrefix = s3["BasePrefix"] ?? "";

        var cfg = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true, // Required for MinIO
        };
        _client = new AmazonS3Client(accessKey, secretKey, cfg);
    }

    private string BuildKey(string key) =>
        string.IsNullOrEmpty(_basePrefix) ? key : $"{_basePrefix.TrimEnd('/')}/{key}";

    public async Task StoreAsync(Stream content, string key, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = BuildKey(key),
            InputStream = content,
            AutoCloseStream = false,
        };
        await _client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(
            new GetObjectRequest { BucketName = _bucketName, Key = BuildKey(key) }, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucketName, BuildKey(key), ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        { /* no-op */ }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucketName, BuildKey(key), ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>Downloads the object to a temp directory and returns a lease that cleans it up.</summary>
    public async Task<LocalFileLease> EnsureLocalAsync(string key, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gpx-s3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, Path.GetFileName(key));
        await using var stream = await GetAsync(key, ct);
        await using var file = File.Create(tempPath);
        await stream.CopyToAsync(file, ct);
        return new LocalFileLease(tempPath, tempDir);
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = BuildKey(key),
            Expires = DateTime.UtcNow.Add(expiry),
        };
        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    /// <summary>Creates the S3 bucket if it doesn't already exist (idempotent).</summary>
    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        { /* already exists */ }
    }

    public void Dispose() => _client.Dispose();
}
