using System.Globalization;
using GpxAnalyzer.Cli.Stats;

namespace GpxAnalyzer.Cli.Output;

public sealed class TextFormatter : IFormatter
{
    public void Format(TextWriter w, string filename, Summary s, StopConfig cfg)
    {
        w.WriteLine();
        w.WriteLine($"=== GPX Analysis: {filename} ===");

        PrintGeneralTable(w, s);
        PrintDistanceTable(w, s);
        PrintElevationTable(w, s);
        PrintStopTable(w, s, cfg);
        PrintBiometricsTable(w, s);
    }

    private static void PrintGeneralTable(TextWriter w, Summary s)
    {
        w.WriteLine();
        w.WriteLine("General");
        var rows = new List<string[]>
        {
            new[] { "Start Time", s.StartTime.ToString("yyyy-MM-dd HH:mm:ss UTC") },
            new[] { "End Time", s.EndTime.ToString("yyyy-MM-dd HH:mm:ss UTC") },
            new[] { "Total Time", FormatHelpers.FormatDuration(s.TotalTime) },
            new[] { "Moving Time", FormatHelpers.FormatDuration(s.MovingTime) },
            new[] { "Stopped Time", FormatHelpers.FormatDuration(s.StoppedTime) },
            new[] { "Points", s.PointCount.ToString() },
        };
        if (s.FilteredPoints > 0)
            rows.Add(new[] { "Filtered Points", s.FilteredPoints.ToString() });
        rows.Add(new[] { "Segments", s.SegmentCount.ToString() });
        rows.Add(new[] { "Points/km", s.PointsPerKm.ToString("F1", CultureInfo.InvariantCulture) });
        RenderTable(w, rows);
    }

    private static void PrintDistanceTable(TextWriter w, Summary s)
    {
        w.WriteLine();
        w.WriteLine("Distance & Speed");
        RenderTable(w, new List<string[]>
        {
            new[] { "Total Distance (2D)", FormatHelpers.FormatDistance(s.TotalDistance) },
            new[] { "Total Distance (3D)", FormatHelpers.FormatDistance(s.TotalDistance3D) },
            new[] { "Avg Speed", FormatHelpers.FormatSpeed(s.Speed.AvgSpeed) },
            new[] { "Avg Moving Speed", FormatHelpers.FormatSpeed(s.Speed.AvgMovingSpeed) },
            new[] { "Max Speed", FormatHelpers.FormatSpeed(s.Speed.MaxSpeed) },
            new[] { "Avg Pace", FormatHelpers.FormatPace(s.Speed.AvgPace) },
            new[] { "Avg Moving Pace", FormatHelpers.FormatPace(s.Speed.AvgMovingPace) },
        });
    }

    private static void PrintElevationTable(TextWriter w, Summary s)
    {
        w.WriteLine();
        w.WriteLine("Elevation");
        RenderTable(w, new List<string[]>
        {
            new[] { "Elevation Gain", $"+{FormatHelpers.FormatElevation(s.Elevation.Gain)}" },
            new[] { "Elevation Loss", $"-{FormatHelpers.FormatElevation(s.Elevation.Loss)}" },
            new[] { "Max Elevation", FormatHelpers.FormatElevation(s.Elevation.Max) },
            new[] { "Min Elevation", FormatHelpers.FormatElevation(s.Elevation.Min) },
        });
    }

    private static void PrintStopTable(TextWriter w, Summary s, StopConfig cfg)
    {
        w.WriteLine();
        w.WriteLine($"Stops (speed < {cfg.MaxSpeed.ToString("F1", CultureInfo.InvariantCulture)} m/s, min duration: {FormatHelpers.FormatDuration(cfg.MinDuration)})");

        if (s.StopCount == 0)
        {
            w.WriteLine("  No stops detected.");
            return;
        }

        var rows = new List<string[]>
        {
            new[] { "Stop Count", s.StopCount.ToString() },
            new[] { "Total Stopped Time", FormatHelpers.FormatDuration(s.TotalStopTime) },
            new[] { "Avg Stop Duration", FormatHelpers.FormatDuration(s.AvgStopDuration) },
        };
        if (s.LongestStop != null)
        {
            rows.Add(new[] { "Longest Stop",
                $"{FormatHelpers.FormatDuration(s.LongestStop.Duration)} ({s.LongestStop.StartTime:yyyy-MM-dd HH:mm})" });
        }
        RenderTable(w, rows);
    }

    private static void PrintBiometricsTable(TextWriter w, Summary s)
    {
        var bio = s.Biometrics;
        if (bio.HeartRate == null && bio.Power == null &&
            bio.Cadence == null && bio.Temperature == null)
            return;

        w.WriteLine();
        w.WriteLine("Biometrics");
        var rows = new List<string[]>();

        if (bio.HeartRate is { } hr)
        {
            rows.Add(new[] { "Avg Heart Rate", hr.Avg.ToString("F0", CultureInfo.InvariantCulture) + " bpm" });
            rows.Add(new[] { "Max Heart Rate", $"{hr.Max} bpm" });
            rows.Add(new[] { "Min Heart Rate", $"{hr.Min} bpm" });
            foreach (var z in hr.Zones)
                rows.Add(new[] { $"  {z.Name}", FormatHelpers.FormatDuration(z.Duration) });
        }
        if (bio.Power is { } pw)
        {
            rows.Add(new[] { "Avg Power", pw.Avg.ToString("F0", CultureInfo.InvariantCulture) + " W" });
            rows.Add(new[] { "Max Power", $"{pw.Max} W" });
            rows.Add(new[] { "Normalized Power", pw.NormalizedPower.ToString("F0", CultureInfo.InvariantCulture) + " W" });
        }
        if (bio.Cadence is { } cad)
        {
            rows.Add(new[] { "Avg Cadence", cad.Avg.ToString("F0", CultureInfo.InvariantCulture) + " rpm" });
            rows.Add(new[] { "Max Cadence", $"{cad.Max} rpm" });
        }
        if (bio.Temperature is { } temp)
        {
            rows.Add(new[] { "Avg Temperature", temp.Avg.ToString("F1", CultureInfo.InvariantCulture) + " °C" });
            rows.Add(new[] { "Min Temperature", temp.Min.ToString("F1", CultureInfo.InvariantCulture) + " °C" });
            rows.Add(new[] { "Max Temperature", temp.Max.ToString("F1", CultureInfo.InvariantCulture) + " °C" });
        }

        RenderTable(w, rows);
    }

    /// <summary>
    /// Renders a simple two-column ASCII table matching Go's tablewriter default format.
    /// </summary>
    private static void RenderTable(TextWriter w, List<string[]> rows)
    {
        if (rows.Count == 0) return;

        int col0 = 0, col1 = 0;
        foreach (var row in rows)
        {
            if (row[0].Length > col0) col0 = row[0].Length;
            if (row[1].Length > col1) col1 = row[1].Length;
        }

        string sep = "+" + new string('-', col0 + 2) + "+" + new string('-', col1 + 2) + "+";
        w.WriteLine(sep);
        foreach (var row in rows)
        {
            w.WriteLine($"| {row[0].PadRight(col0)} | {row[1].PadRight(col1)} |");
        }
        w.WriteLine(sep);
    }
}
