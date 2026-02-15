using System.Globalization;
using System.Text;

namespace GpxAnalyzer.Cli.Benchmark;

public static class BenchmarkOutput
{
    public static readonly HashSet<string> ValidSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "distance", "distance-3d", "elev-gain", "elev-loss",
        "moving-time", "avg-speed", "max-speed", "stops", "filtered", "time"
    };

    public static void WriteTable(TextWriter w, List<RunResult> results, string filename, int pointCount)
    {
        w.WriteLine();
        w.WriteLine($"=== Benchmark: {filename} ({pointCount} points) ===");
        w.WriteLine($"Configurations: {results.Count}");
        w.WriteLine();

        if (results.Count == 0)
        {
            w.WriteLine("No results.");
            return;
        }

        var rawHeaders = RunResult.Headers();
        // Auto-format headers like Go's tablewriter: uppercase + spaces around punctuation
        var headers = new string[rawHeaders.Length];
        for (int i = 0; i < rawHeaders.Length; i++)
            headers[i] = AutoFormatHeader(rawHeaders[i]);

        var allRows = new List<string[]>(results.Count);
        for (int i = 0; i < results.Count; i++)
            allRows.Add(results[i].Row(i));

        // Compute column widths
        var widths = new int[headers.Length];
        for (int c = 0; c < headers.Length; c++)
        {
            widths[c] = headers[c].Length;
            foreach (var row in allRows)
            {
                if (c < row.Length && row[c].Length > widths[c])
                    widths[c] = row[c].Length;
            }
        }

        // Top border
        w.WriteLine(BorderLine(widths, '┌', '┬', '┐'));
        // Header row (center-aligned like Go's tablewriter)
        WriteCenteredRow(w, headers, widths, '│');
        // Header/body separator
        w.WriteLine(BorderLine(widths, '├', '┼', '┤'));
        // Data rows (left-aligned)
        foreach (var row in allRows)
            WriteRow(w, row, widths, '│');
        // Bottom border
        w.WriteLine(BorderLine(widths, '└', '┴', '┘'));

        // Footer: totalDuration = sum of individual ComputeDurations (like Go)
        var totalDuration = TimeSpan.Zero;
        foreach (var r in results)
            totalDuration += r.ComputeDuration;
        long avgMs = totalDuration.Ticks > 0 ? (long)totalDuration.TotalMilliseconds / results.Count : 0;
        w.WriteLine();
        w.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Completed {0} runs in {1:F1}s (avg: {2}ms/run)",
            results.Count, totalDuration.TotalSeconds, avgMs));
    }

    private static string BorderLine(int[] widths, char left, char mid, char right)
    {
        var sb = new StringBuilder();
        sb.Append(left);
        for (int c = 0; c < widths.Length; c++)
        {
            if (c > 0) sb.Append(mid);
            sb.Append(new string('─', widths[c] + 2));
        }
        sb.Append(right);
        return sb.ToString();
    }

    private static void WriteCenteredRow(TextWriter w, string[] row, int[] widths, char border)
    {
        var sb = new StringBuilder();
        sb.Append(border);
        for (int c = 0; c < widths.Length; c++)
        {
            string val = c < row.Length ? row[c] : "";
            int pad = widths[c] - val.Length;
            int left = pad / 2;
            int right = pad - left;
            sb.Append(' ').Append(new string(' ', left)).Append(val).Append(new string(' ', right)).Append(' ').Append(border);
        }
        w.WriteLine(sb);
    }

    private static void WriteRow(TextWriter w, string[] row, int[] widths, char border)
    {
        var sb = new StringBuilder();
        sb.Append(border);
        for (int c = 0; c < widths.Length; c++)
        {
            string val = c < row.Length ? row[c] : "";
            sb.Append(' ').Append(val.PadRight(widths[c])).Append(' ').Append(border);
        }
        w.WriteLine(sb);
    }

    public static void WriteCsv(string path, List<RunResult> results)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine(string.Join(",", RunResult.Headers()));
        for (int i = 0; i < results.Count; i++)
            sw.WriteLine(string.Join(",", results[i].Row(i)));
    }

    /// <summary>
    /// Replicates Go tablewriter's autoformat: uppercase + spaces between different char classes.
    /// E.g. "E.Smooth" → "E . SMOOTH", "Dist 2D" → "DIST 2 D", "D+" → "D +"
    /// </summary>
    private static string AutoFormatHeader(string header)
    {
        var upper = header.ToUpperInvariant();
        var sb = new StringBuilder();
        for (int i = 0; i < upper.Length; i++)
        {
            char c = upper[i];
            if (i > 0)
            {
                char prev = upper[i - 1];
                bool addSpace =
                    (char.IsLetterOrDigit(prev) && !char.IsLetterOrDigit(c) && c != ' ') ||
                    (!char.IsLetterOrDigit(prev) && prev != ' ' && char.IsLetterOrDigit(c)) ||
                    (char.IsLetter(prev) && char.IsDigit(c)) ||
                    (char.IsDigit(prev) && char.IsLetter(c));
                if (addSpace)
                    sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static void SortResults(List<RunResult> results, string column)
    {
        results.Sort((a, b) => column.ToLowerInvariant() switch
        {
            "distance" => a.Distance2D.CompareTo(b.Distance2D),
            "distance-3d" => a.Distance3D.CompareTo(b.Distance3D),
            "elev-gain" => a.ElevGain.CompareTo(b.ElevGain),
            "elev-loss" => a.ElevLoss.CompareTo(b.ElevLoss),
            "moving-time" => a.MovingTime.CompareTo(b.MovingTime),
            "avg-speed" => a.AvgSpeed.CompareTo(b.AvgSpeed),
            "max-speed" => a.MaxSpeed.CompareTo(b.MaxSpeed),
            "stops" => a.StopCount.CompareTo(b.StopCount),
            "filtered" => a.FilteredPoints.CompareTo(b.FilteredPoints),
            "time" => a.ComputeDuration.CompareTo(b.ComputeDuration),
            _ => 0
        });
    }
}
