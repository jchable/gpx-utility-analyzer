using System.IO.Compression;
using System.Text;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Storage;

namespace GpxAnalyzer.Api.Tests.Storage;

/// <summary>
/// ExtractOriginalToTempAsync used entry.FullName straight from the archive, so an
/// entry naming "../../x" wrote outside the temp directory it was handed (Zip Slip,
/// CodeQL cs/zipslip). We write these archives ourselves, but they round-trip through
/// object storage other systems can reach, so extraction validates rather than trusts.
/// </summary>
public class ZipSlipTests : IDisposable
{
    private readonly string _storageDir =
        Path.Combine(Path.GetTempPath(), $"zipslip-store-{Guid.NewGuid():N}");

    public ZipSlipTests() => Directory.CreateDirectory(_storageDir);

    public void Dispose()
    {
        try { Directory.Delete(_storageDir, recursive: true); } catch { /* best-effort */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>A storage backed by a throwaway directory, holding one archive.</summary>
    private sealed class DirectoryStorage(string root) : IStorageService
    {
        public Task StoreAsync(Stream content, string key, CancellationToken ct = default)
        {
            using var file = File.Create(Path.Combine(root, key));
            content.CopyTo(file);
            return Task.CompletedTask;
        }

        public Task<Stream> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream>(File.OpenRead(Path.Combine(root, key)));

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            File.Delete(Path.Combine(root, key));
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
            => Task.FromResult(File.Exists(Path.Combine(root, key)));

        public Task<LocalFileLease> EnsureLocalAsync(string key, CancellationToken ct = default)
            => Task.FromResult(new LocalFileLease(Path.Combine(root, key)));

        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary>Writes {name}_original.zip into storage containing a single entry.</summary>
    private void WriteArchive(string archiveKey, string entryName, string content)
    {
        var path = Path.Combine(_storageDir, archiveKey);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry(entryName);
        using var stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    [Theory]
    [InlineData("../escaped.gpx")]
    [InlineData("../../escaped.gpx")]
    [InlineData("sub/../../escaped.gpx")]
    public async Task ExtractOriginalToTemp_WithAnEntryPointingOutsideTheTempDir_Throws(string entryName)
    {
        WriteArchive("track_original.zip", entryName, "<gpx/>");
        var service = new GpxStorageService(new DirectoryStorage(_storageDir));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractOriginalToTempAsync("track.gpx"));
        Assert.Contains("escapes", ex.Message, StringComparison.OrdinalIgnoreCase);

        // The escape target sits one level above the extraction root, i.e. in the
        // system temp directory. Nothing may have been written there.
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "escaped.gpx")),
            "the entry was extracted outside the temp directory");
    }

    [Fact]
    public async Task ExtractOriginalToTemp_WithAnOrdinaryEntry_StillExtracts()
    {
        WriteArchive("track_original.zip", "track.gpx", "<gpx>ok</gpx>");
        var service = new GpxStorageService(new DirectoryStorage(_storageDir));

        using var lease = await service.ExtractOriginalToTempAsync("track.gpx");

        Assert.Equal("<gpx>ok</gpx>", await File.ReadAllTextAsync(lease.Path));
    }
}
