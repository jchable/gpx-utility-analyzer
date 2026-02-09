namespace GpxAnalyzer.Api.Services;

using System.Diagnostics;
using System.Text.Json;
using GpxAiAnalyzer.Core.Models;

public class GpxCliService
{
    private readonly string _binaryPath;
    private readonly string _defaultPreset;
    private readonly string _defaultSmoothing;
    private readonly ILogger<GpxCliService> _logger;

    public GpxCliService(IConfiguration configuration, ILogger<GpxCliService> logger)
    {
        _binaryPath = configuration["GpxCli:BinaryPath"] ?? "gpx-analyzer";
        _defaultPreset = configuration["GpxCli:DefaultPreset"] ?? "hiking";
        _defaultSmoothing = configuration["GpxCli:DefaultSmoothing"] ?? "medium";
        _logger = logger;
    }

    public async Task<GpxStats> AnalyzeAsync(string gpxFilePath, CancellationToken ct = default)
    {
        var args = $"analyze \"{gpxFilePath}\" --format json --preset {_defaultPreset} --smoothing {_defaultSmoothing}";

        _logger.LogInformation("Running: {Binary} {Args}", _binaryPath, args);

        var psi = new ProcessStartInfo
        {
            FileName = _binaryPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start gpx-analyzer process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogDebug("gpx-analyzer stderr: {StdErr}", stderr.Trim());

        if (process.ExitCode != 0)
        {
            _logger.LogError("gpx-analyzer exited with code {Code}: {StdErr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"gpx-analyzer failed (exit code {process.ExitCode}): {stderr}");
        }

        _logger.LogDebug("gpx-analyzer stdout length: {Length} chars", stdout.Length);

        var stats = JsonSerializer.Deserialize<GpxStats>(stdout, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize gpx-analyzer output.");

        return stats;
    }
}
