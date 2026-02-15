using System.Globalization;

namespace GpxAnalyzer.Cli.Benchmark;

public sealed class RunResult
{
    public BenchmarkCombination Combination { get; init; } = new();
    public double Distance2D { get; init; }   // km
    public double Distance3D { get; init; }   // km
    public double ElevGain { get; init; }     // m
    public double ElevLoss { get; init; }     // m
    public double ElevMax { get; init; }      // m
    public double ElevMin { get; init; }      // m
    public TimeSpan MovingTime { get; init; }
    public TimeSpan StoppedTime { get; init; }
    public int StopCount { get; init; }
    public double AvgSpeed { get; init; }     // km/h
    public double MaxSpeed { get; init; }     // km/h
    public int FilteredPoints { get; init; }
    public TimeSpan ComputeDuration { get; init; }

    public static string[] Headers() =>
    [
        "#", "Preset", "Algo", "E.Smooth", "T.Smooth", "DEM", "Params",
        "Dist 2D", "Dist 3D", "D+", "D-", "Max Ele", "Min Ele",
        "Moving", "Stopped", "Stops", "Avg Spd", "Max Spd", "Filtered", "Time"
    ];

    public string[] Row(int index) =>
    [
        (index + 1).ToString(),
        Combination.Preset,
        Combination.ElevAlgo,
        Combination.ElevSmoothing,
        Combination.TrackSmoothing,
        Combination.UseDem ? "yes" : "no",
        Combination.ParamsLabel(),
        Distance2D.ToString("F2", CultureInfo.InvariantCulture) + " km",
        Distance3D.ToString("F2", CultureInfo.InvariantCulture) + " km",
        "+" + ElevGain.ToString("F0", CultureInfo.InvariantCulture) + " m",
        "-" + ElevLoss.ToString("F0", CultureInfo.InvariantCulture) + " m",
        ElevMax.ToString("F0", CultureInfo.InvariantCulture) + " m",
        ElevMin.ToString("F0", CultureInfo.InvariantCulture) + " m",
        FormatDur(MovingTime),
        FormatDur(StoppedTime),
        StopCount.ToString(),
        AvgSpeed.ToString("F1", CultureInfo.InvariantCulture) + " km/h",
        MaxSpeed.ToString("F1", CultureInfo.InvariantCulture) + " km/h",
        FilteredPoints.ToString(),
        $"{(long)ComputeDuration.TotalMilliseconds}ms"
    ];

    private static string FormatDur(TimeSpan d)
    {
        int h = (int)d.TotalHours;
        int m = d.Minutes;
        int s = d.Seconds;
        if (h > 0)
            return $"{h}h{m:D2}m";
        return $"{m}m{s:D2}s";
    }
}
