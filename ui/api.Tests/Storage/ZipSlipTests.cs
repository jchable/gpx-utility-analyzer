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

    /// <summary>Writes {name}_original.zip into storage containing the given entries in order.</summary>
    private void WriteArchive(string archiveKey, params string[] entryNames)
    {
        var path = Path.Combine(_storageDir, archiveKey);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in entryNames)
        {
            var entry = zip.CreateEntry(name);
            // A name ending in '/' is a directory entry and carries no content.
            if (name.EndsWith('/')) continue;
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes("<gpx>ok</gpx>"));
        }
    }

    [Theory]
    [InlineData("../")]
    [InlineData("../../")]
    [InlineData("sub/../../")]
    public async Task ExtractOriginalToTemp_WithAnEntryPointingOutsideTheTempDir_Throws(string traversal)
    {
        // Unique per case. The guard is proved by this file's absence, so a leftover
        // from an earlier run or a sibling case must not be what decides the assertion.
        var marker = $"escaped-{Guid.NewGuid():N}.gpx";
        WriteArchive("track_original.zip", traversal + marker);
        var service = new GpxStorageService(new DirectoryStorage(_storageDir));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractOriginalToTempAsync("track.gpx"));
        Assert.Contains("escapes", ex.Message, StringComparison.OrdinalIgnoreCase);

        // The extraction root sits directly under the system temp directory, so one
        // "../" lands in temp itself and two land in its parent. Check both rather
        // than guessing which depth this case uses.
        var tempRoot = new DirectoryInfo(Path.GetTempPath());
        foreach (var dir in new[] { tempRoot, tempRoot.Parent })
        {
            if (dir is null) continue;
            Assert.False(File.Exists(Path.Combine(dir.FullName, marker)),
                $"the entry was extracted outside the extraction root, into {dir.FullName}");
        }
    }

    [Fact]
    public async Task ExtractOriginalToTemp_WithAnOrdinaryEntry_StillExtracts()
    {
        WriteArchive("track_original.zip", "track.gpx");
        var service = new GpxStorageService(new DirectoryStorage(_storageDir));

        using var lease = await service.ExtractOriginalToTempAsync("track.gpx");

        Assert.Equal("<gpx>ok</gpx>", await File.ReadAllTextAsync(lease.Path));
    }

    /// <summary>
    /// A tampered archive can open with a directory entry. Taking it blindly sent an
    /// undefined exception out of ExtractToFile, which reaches the caller as a 500.
    /// </summary>
    [Fact]
    public async Task ExtractOriginalToTemp_WhenADirectoryEntryComesFirst_TakesTheFileAfterIt()
    {
        WriteArchive("track_original.zip", "sub/", "track.gpx");
        var service = new GpxStorageService(new DirectoryStorage(_storageDir));

        using var lease = await service.ExtractOriginalToTempAsync("track.gpx");

        Assert.Equal("track.gpx", Path.GetFileName(lease.Path));
        Assert.Equal("<gpx>ok</gpx>", await File.ReadAllTextAsync(lease.Path));
    }

    [Fact]
    public async Task ExtractOriginalToTemp_WithNoFileEntries_ThrowsAClearError()
    {
        WriteArchive("track_original.zip", "sub/");
        var service = new GpxStorageService(new DirectoryStorage(_storageDir));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractOriginalToTempAsync("track.gpx"));
        Assert.Contains("no file entries", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
