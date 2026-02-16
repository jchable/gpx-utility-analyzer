using System.Globalization;

namespace GpxAnalyzer.Cli.Core.Output;

public static class FormatHelpers
{
    /// <summary>
    /// Formats a duration as "2d 5h 30m 15s" (matching Go's FormatDuration exactly).
    /// </summary>
    public static string FormatDuration(TimeSpan d)
    {
        if (d <= TimeSpan.Zero)
            return "0s";

        int totalSeconds = (int)d.TotalSeconds;
        int days = totalSeconds / 86400;
        int hours = (totalSeconds % 86400) / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (days > 0)
            return $"{days}d {hours}h {minutes}m {seconds}s";
        if (hours > 0)
            return $"{hours}h {minutes}m {seconds}s";
        if (minutes > 0)
            return $"{minutes}m {seconds}s";
        return $"{seconds}s";
    }

    /// <summary>
    /// Formats pace as "min:sec min/km" or "-" if zero.
    /// </summary>
    public static string FormatPace(TimeSpan d)
    {
        if (d <= TimeSpan.Zero)
            return "-";
        int totalSeconds = (int)d.TotalSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2} min/km";
    }

    /// <summary>
    /// Formats distance in meters/km with 1 decimal.
    /// </summary>
    public static string FormatDistance(double meters)
    {
        if (meters < 1000)
            return meters.ToString("F0", CultureInfo.InvariantCulture) + " m";
        return (meters / 1000).ToString("F1", CultureInfo.InvariantCulture) + " km";
    }

    /// <summary>
    /// Formats speed in m/s as km/h with 1 decimal.
    /// </summary>
    public static string FormatSpeed(double mps) =>
        (mps * 3.6).ToString("F1", CultureInfo.InvariantCulture) + " km/h";

    /// <summary>
    /// Formats elevation in meters with no decimals.
    /// </summary>
    public static string FormatElevation(double meters) =>
        meters.ToString("F0", CultureInfo.InvariantCulture) + " m";
}
