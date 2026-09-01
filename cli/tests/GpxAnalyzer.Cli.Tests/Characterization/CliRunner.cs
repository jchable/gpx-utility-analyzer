using System.Diagnostics;
using System.Text;

namespace GpxAnalyzer.Cli.Tests.Characterization;

internal sealed record CliResult(string StdOut, string StdErr, int ExitCode);

/// <summary>
/// Extra control over a single <see cref="CliRunner"/> invocation. Everything here is
/// optional; <see cref="CliRunner.Run(string[])"/> is the no-options shorthand.
/// </summary>
internal sealed record CliOptions
{
    /// <summary>
    /// Runs against the freshly seeded working directory just before the CLI starts,
    /// e.g. to drop DEM tiles or extra input files into it.
    /// </summary>
    internal Action<string>? Arrange { get; init; }

    /// <summary>
    /// Runs against the working directory once the CLI has exited and before the
    /// directory is deleted, so a test can assert on the files the CLI wrote.
    /// </summary>
    internal Action<string>? Inspect { get; init; }

    /// <summary>
    /// Extra environment variables for the child, on top of the sandbox
    /// <see cref="CliRunner"/> always installs. It is a factory rather than a dictionary
    /// because the useful values are paths inside the per-run working directory passed in.
    /// </summary>
    internal Func<string, IReadOnlyDictionary<string, string>>? Environment { get; init; }
}

/// <summary>
/// Runs the built gpx-analyzer executable out-of-process inside a throwaway working
/// directory seeded with the testdata GPX fixtures, so that every path the CLI echoes
/// back is relative and therefore identical on Windows and Linux.
///
/// Every run is also SANDBOXED: see <see cref="SandboxEnvironment"/>. No test has to remember
/// to ask, and none can forget.
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

    /// <summary>The sandbox home directory's name inside a working directory.</summary>
    private const string HomeDirName = "home";

    /// <summary>
    /// Everything <see cref="Run(string[])"/> puts in a working directory before the CLI starts,
    /// sorted: the GPX fixtures plus the sandbox home. A test that asserts the CLI wrote nothing
    /// compares the directory against this.
    /// </summary>
    internal static IReadOnlyList<string> SeededFixtures { get; } =
        [.. Directory.GetFiles(TestDataDir, "*.gpx").Select(Path.GetFileName)
            .Append(HomeDirName).Order(StringComparer.Ordinal)!];

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

    /// <summary>
    /// The sandboxed home directory inside a run's throwaway working directory. Created before
    /// every run; <see cref="SandboxEnvironment"/> points the platform cache lookups at it.
    /// </summary>
    internal static string HomeIn(string workDir) => Path.Combine(workDir, HomeDirName);

    /// <summary>
    /// The sandbox every run gets, whether or not it asked (#135).
    ///
    /// DemSource.DefaultCacheDir() resolves under LOCALAPPDATA on Windows and under
    /// LocalApplicationData / $HOME elsewhere, and any run that builds a DEM source without an
    /// explicit --dem-cache falls back to it - including `--dem-dir x --dem-auto-download
    /// false`. Unsandboxed that is the DEVELOPER'S OWN SRTM cache: on a machine holding
    /// N48E002.hgt, small.gpx reports a max elevation of 46.26 m where its own data says 43.33,
    /// so the suite passes or fails according to what the developer happens to have downloaded.
    /// Redirecting the cache into the throwaway working directory makes that impossible.
    ///
    /// The proxy variables are the second line of defence behind
    /// <see cref="BlockDownloadsIntoThePlatformCache"/>: should a download ever get as far as
    /// HTTP, it goes to a refused loopback port rather than to the real SRTM mirror.
    /// </summary>
    private static Dictionary<string, string> SandboxEnvironment(string workDir)
    {
        var home = HomeIn(workDir);
        return new Dictionary<string, string>
        {
            ["LOCALAPPDATA"] = home,        // DefaultCacheDir() on Windows
            ["XDG_DATA_HOME"] = home,       // ... and via GetFolderPath(LocalApplicationData) on Unix
            ["HOME"] = home,
            ["HTTP_PROXY"] = "http://127.0.0.1:1",
            ["HTTPS_PROXY"] = "http://127.0.0.1:1",
            ["ALL_PROXY"] = "http://127.0.0.1:1",
            ["NO_PROXY"] = "",
        };
    }

    /// <summary>
    /// Kills auto-download before a socket is ever opened, for every run that did not build a
    /// platform cache of its own.
    ///
    /// --dem-auto-download defaults to TRUE, so a run that says nothing about DEM downloads a
    /// ~25 MB SRTM tile from the real mirror: measured at 1.9 s here with the environment
    /// sandbox alone, and the tile lands in the sandboxed cache where the next assertion
    /// silently uses it. Redirecting the cache is not enough on its own.
    ///
    /// TileDownloader.DownloadTileAsync calls Directory.CreateDirectory on the tile's shard
    /// folder before it constructs an HttpClient, so occupying the cache root's name with a
    /// regular FILE makes it throw instantly - structurally, not by timeout. A test that
    /// genuinely wants a populated platform cache creates the directory in Arrange, and this
    /// then leaves it alone.
    /// </summary>
    private static void BlockDownloadsIntoThePlatformCache(string workDir)
    {
        var home = HomeIn(workDir);
        string[] roots =
        [
            Path.Combine(home, "gpx-utility-analyzer"),                    // LOCALAPPDATA / XDG_DATA_HOME
            Path.Combine(home, ".local", "share", "gpx-utility-analyzer"), // $HOME fallback
        ];

        foreach (var root in roots)
        {
            if (Directory.Exists(root) || File.Exists(root))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            File.WriteAllText(root, "occupies the DEM cache root so no download can create it");
        }
    }

    internal static CliResult Run(params string[] args) => Run(new CliOptions(), args);

    internal static CliResult Run(CliOptions options, params string[] args)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "gpx-char-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            Directory.CreateDirectory(HomeIn(workDir));
            foreach (var gpx in Directory.GetFiles(TestDataDir, "*.gpx"))
                File.Copy(gpx, Path.Combine(workDir, Path.GetFileName(gpx)));

            options.Arrange?.Invoke(workDir);
            BlockDownloadsIntoThePlatformCache(workDir);

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
            foreach (var (key, value) in SandboxEnvironment(workDir))
                psi.Environment[key] = value;
            if (options.Environment != null)
                foreach (var (key, value) in options.Environment(workDir))
                    psi.Environment[key] = value;

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

            var result = new CliResult(
                stdoutTask.GetAwaiter().GetResult(),
                stderrTask.GetAwaiter().GetResult(),
                process.ExitCode);

            options.Inspect?.Invoke(workDir);
            return result;
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
