using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Stats;

/// <summary>
/// Elevation gain/loss via linear segment fitting.
/// </summary>
public static class ElevationSegments
{
    public static ElevationResult Compute(List<TrackPoint> points, double minSegLen, double maxSlopeDev)
    {
        if (points.Count == 0)
            return new ElevationResult();

        var result = new ElevationResult
        {
            Max = points[0].Ele,
            Min = points[0].Ele
        };

        // Max/min on all original points
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].Ele > result.Max) result.Max = points[i].Ele;
            if (points[i].Ele < result.Min) result.Min = points[i].Ele;
        }

        if (points.Count < 2)
            return result;

        var profile = ProfileBuilder.Build(points);
        var segments = FindSegments(profile, minSegLen, maxSlopeDev);

        // D+/D- from fitted segment endpoints
        foreach (var seg in segments)
        {
            double delta = seg.EndEle - seg.StartEle;
            if (delta > 0)
                result.Gain += delta;
            else
                result.Loss += -delta;
        }

        return result;
    }

    private static List<FittedSegment> FindSegments(ProfilePoint[] profile, double minSegLen, double maxSlopeDev)
    {
        if (profile.Length < 2)
            return [];

        var segments = new List<FittedSegment>();
        int segStart = 0;

        while (segStart < profile.Length - 1)
        {
            int segEnd = segStart + 1;

            // Extend the segment as far as possible
            while (segEnd < profile.Length)
            {
                double segLen = profile[segEnd].CumDist - profile[segStart].CumDist;
                var sub = profile.AsSpan(segStart, segEnd - segStart + 1);
                var (slope, intercept) = LinearFit(sub);
                double rms = RmsResidual(sub, slope, intercept);

                if (segLen >= minSegLen && rms > maxSlopeDev)
                {
                    segEnd--;
                    break;
                }
                segEnd++;
            }

            // Clamp
            if (segEnd >= profile.Length) segEnd = profile.Length - 1;
            if (segEnd <= segStart)
            {
                segEnd = segStart + 1;
                if (segEnd >= profile.Length) break;
            }

            // Fit the final segment
            var finalSub = profile.AsSpan(segStart, segEnd - segStart + 1);
            var (finalSlope, finalIntercept) = LinearFit(finalSub);
            double startEle = finalSlope * profile[segStart].CumDist + finalIntercept;
            double endEle = finalSlope * profile[segEnd].CumDist + finalIntercept;

            segments.Add(new FittedSegment(segStart, segEnd, startEle, endEle));
            segStart = segEnd;
        }

        return segments;
    }

    private static (double Slope, double Intercept) LinearFit(ReadOnlySpan<ProfilePoint> profile)
    {
        double n = profile.Length;
        if (n < 2)
            return (0, profile[0].Ele);

        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        foreach (var p in profile)
        {
            sumX += p.CumDist;
            sumY += p.Ele;
            sumXY += p.CumDist * p.Ele;
            sumX2 += p.CumDist * p.CumDist;
        }

        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-12)
            return (0, sumY / n);

        double slope = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;
        return (slope, intercept);
    }

    private static double RmsResidual(ReadOnlySpan<ProfilePoint> profile, double slope, double intercept)
    {
        if (profile.Length == 0)
            return 0;

        double sumSq = 0;
        foreach (var p in profile)
        {
            double predicted = slope * p.CumDist + intercept;
            double residual = p.Ele - predicted;
            sumSq += residual * residual;
        }
        return Math.Sqrt(sumSq / profile.Length);
    }

    private readonly record struct FittedSegment(int StartIdx, int EndIdx, double StartEle, double EndEle);
}
