using System.Diagnostics;
using System.Text;

namespace GpxAnalyzer.Cli.Tests.Characterization;

internal sealed record CliResult(string StdOut, string StdErr, int ExitCode);

/// <summary>
/// Runs the built gpx-analyzer executable out-of-process inside a throwaway working
/// directory seeded with the testdata GPX fixtures, so that every path the CLI echoes
/// back is relative and therefore identical on Windows and Linux.
/// </summary>
internal static class CliRunner
{
    // AppContext.BaseDirectory = <proj>/bin/<Configuration>/net9.0/
    private static readonly string TestProjectDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    private static readonly string Configuration =
        new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    private static readonly string TestDataDir = Path.Combine(TestProjectDir, "testdata");

    internal static string GoldenDir => Path.Combine(TestDataDir, "golden");

    private static readonly Lazy<string> Executable = new(FindExecutable);

    private static string FindExecutable()
    {
        // <repo>/cli/tests/GpxAnalyzer.Cli.Tests -> <repo>/cli/src/GpxAnalyzer.Cli/bin/<cfg>/net9.0
        var binDir = Path.GetFullPath(Path.Combine(
            TestProjectDir, "..", "..", "src", "GpxAnalyzer.Cli", "bin", Configuration, "net9.0"));

        if (!Directory.Exists(binDir))
            throw new InvalidOperationException(
                $"gpx-analyzer output directory not found: {binDir}. " +
                "Build it first: dotnet build cli/src/GpxAnalyzer.Cli/");

        // The build puts the apphost under a RID subfolder (win-x64, linux-x64, ...)
        // because the project is SelfContained.
        var name = OperatingSystem.IsWindows() ? "gpx-analyzer.exe" : "gpx-analyzer";
        var matches = Directory.GetFiles(binDir, name, SearchOption.AllDirectories);
        if (matches.Length == 0)
            throw new InvalidOperationException($"'{name}' not found under {binDir}.");

        return matches[0];
    }

    internal static CliResult Run(params string[] args)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "gpx-char-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            foreach (var gpx in Directory.GetFiles(TestDataDir, "*.gpx"))
                File.Copy(gpx, Path.Combine(workDir, Path.GetFileName(gpx)));

            var psi = new ProcessStartInfo(Executable.Value)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var process = Process.Start(psi)!;
            // Drain both pipes concurrently: split prints one stderr line per segment
            // and a full pipe buffer would deadlock a sequential ReadToEnd.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"gpx-analyzer {string.Join(' ', args)} timed out after 120s.");
            }

            return new CliResult(
                stdoutTask.GetAwaiter().GetResult(),
                stderrTask.GetAwaiter().GetResult(),
                process.ExitCode);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
