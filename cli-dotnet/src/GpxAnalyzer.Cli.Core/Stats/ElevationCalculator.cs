using GpxAnalyzer.Cli.Core.Gpx;

namespace GpxAnalyzer.Cli.Core.Stats;

/// <summary>
/// Identifies which elevation algorithm to use.
/// </summary>
public static class ElevationAlgo
{
    public const string Threshold = "threshold";
    public const string DouglasPeucker = "douglas-peucker";
    public const string Segments = "segments";

    public static bool IsValid(string algo) =>
        algo is Threshold or DouglasPeucker or Segments;
}

/// <summary>
/// Configuration for elevation algorithms.
/// </summary>
public sealed class ElevationConfig
{
    public string Algo { get; set; } = ElevationAlgo.Threshold;
    public double Threshold { get; set; } = 2.0;    // meters, threshold algo
    public double Epsilon { get; set; } = 3.0;      // meters, DP algo
    public double MinSegLen { get; set; } = 200.0;   // meters, segments algo
    public double MaxSlopeDev { get; set; } = 2.0;   // meters RMS, segments algo

    public static ElevationConfig Default() => new();
}

/// <summary>
/// Elevation gain/loss/max/min result.
/// </summary>
public sealed class ElevationResult
{
    public double Gain { get; set; }
    public double Loss { get; set; }
    public double Max { get; set; }
    public double Min { get; set; }
}

public static class ElevationCalculator
{
    /// <summary>
    /// Dispatches to the configured elevation algorithm.
    /// </summary>
    public static ElevationResult ComputeWithAlgo(List<TrackPoint> points, ElevationConfig cfg)
    {
        return cfg.Algo switch
        {
            ElevationAlgo.DouglasPeucker => ElevationDouglasPeucker.Compute(points, cfg.Epsilon),
            ElevationAlgo.Segments => ElevationSegments.Compute(points, cfg.MinSegLen, cfg.MaxSlopeDev),
            _ => ComputeThreshold(points, cfg.Threshold),
        };
    }

    /// <summary>
    /// Computes elevation gain/loss using threshold algorithm.
    /// Only elevation changes >= threshold are counted.
    /// </summary>
    public static ElevationResult ComputeThreshold(List<TrackPoint> points, double threshold)
    {
        if (points.Count == 0)
            return new ElevationResult();

        var result = new ElevationResult
        {
            Max = points[0].Ele,
            Min = points[0].Ele
        };

        double refEle = points[0].Ele;

        for (int i = 1; i < points.Count; i++)
        {
            double ele = points[i].Ele;

            if (ele > result.Max) result.Max = ele;
            if (ele < result.Min) result.Min = ele;

            double delta = ele - refEle;
            if (Math.Abs(delta) >= threshold)
            {
                if (delta > 0)
                    result.Gain += delta;
                else
                    result.Loss += -delta;
                refEle = ele;
            }
        }

        return result;
    }
}
