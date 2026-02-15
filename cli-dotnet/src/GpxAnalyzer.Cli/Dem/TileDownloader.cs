using System.IO.Compression;

namespace GpxAnalyzer.Cli.Dem;

public static class TileDownloader
{
    private const long ExpectedSrtm1Size = 2L * HgtTile.Srtm1Size * HgtTile.Srtm1Size;
    private const long ExpectedSrtm3Size = 2L * HgtTile.Srtm3Size * HgtTile.Srtm3Size;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(60);
    private const int MaxRetries = 2;

    internal static string BaseUrl = "https://elevation-tiles-prod.s3.amazonaws.com/skadi";

    public static string TileUrl(string key)
    {
        string prefix = key[..3];
        return $"{BaseUrl}/{prefix}/{key}.hgt.gz";
    }

    public static async Task DownloadTileAsync(string key, string destPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        string url = TileUrl(key);
        Exception? lastErr = null;

        using var client = new HttpClient { Timeout = DownloadTimeout };

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(attempt * 1000);

            try
            {
                await DoDownloadAsync(client, url, destPath);
                return;
            }
            catch (Exception ex)
            {
                lastErr = ex;
            }
        }

        // Clean up partial file
        try { File.Delete(destPath); } catch { }
        throw new InvalidOperationException(
            $"Downloading {key} after {MaxRetries + 1} attempts: {lastErr?.Message}", lastErr);
    }

    private static async Task DoDownloadAsync(HttpClient client, string url, string destPath)
    {
        using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        using var gzStream = new GZipStream(await resp.Content.ReadAsStreamAsync(), CompressionMode.Decompress);

        string tmpPath = destPath + ".tmp";
        try
        {
            using (var fs = File.Create(tmpPath))
            {
                await gzStream.CopyToAsync(fs);
            }

            long n = new FileInfo(tmpPath).Length;
            if (n != ExpectedSrtm1Size && n != ExpectedSrtm3Size)
            {
                File.Delete(tmpPath);
                throw new InvalidOperationException(
                    $"Unexpected file size {n} bytes (expected SRTM1 or SRTM3)");
            }

            File.Move(tmpPath, destPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmpPath); } catch { }
            throw;
        }
    }
}
