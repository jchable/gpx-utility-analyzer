using System.Text.RegularExpressions;

namespace GpxAnalyzer.Cli.Tests.Characterization;

internal static class Golden
{
    private static readonly Regex Millis = new(@"\d+ms", RegexOptions.Compiled);
    private static readonly Regex Seconds = new(@"in \d+[.,]\d+s", RegexOptions.Compiled);
    private static readonly Regex NonAscii = new(@"[^\x20-\x7E\n]", RegexOptions.Compiled);
    private static readonly Regex SpaceRuns = new(@"[ ]{2,}", RegexOptions.Compiled);

    /// <summary>CRLF/LF agnostic, single trailing newline.</summary>
    internal static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";

    /// <summary>
    /// The benchmark table embeds per-run wall-clock timings, its column widths are
    /// derived from those timings, and its box-drawing characters are emitted in the
    /// OEM code page on Windows but UTF-8 on Linux. Strip all three sources of noise.
    /// </summary>
    internal static string NormalizeBenchmark(string text)
    {
        var t = Normalize(text);
        t = Millis.Replace(t, "<ms>");
        t = Seconds.Replace(t, "in <s>");
        t = NonAscii.Replace(t, "");
        t = SpaceRuns.Replace(t, " ");
        return string.Join('\n', t.Split('\n').Select(line => line.TrimEnd()));
    }

    /// <summary>
    /// Compares <paramref name="actual"/> against testdata/golden/{name}.txt.
    /// Set UPDATE_GOLDEN=1 to (re)write the golden instead of asserting.
    /// </summary>
    internal static void Verify(string name, string actual)
    {
        Directory.CreateDirectory(CliRunner.GoldenDir);
        var path = Path.Combine(CliRunner.GoldenDir, name + ".txt");

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(path, Normalize(actual));
            return;
        }

        Assert.True(File.Exists(path),
            $"Golden file missing: {path}. Regenerate with UPDATE_GOLDEN=1 and review the diff.");

        Assert.Equal(Normalize(File.ReadAllText(path)), Normalize(actual));
    }
}
