using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Stats;

/// <summary>
/// Elevation gain/loss via Douglas-Peucker simplification of the elevation profile.
/// </summary>
public static class ElevationDouglasPeucker
{
    public static ElevationResult Compute(List<TrackPoint> points, double epsilon)
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

        // Build profile and simplify
        var profile = ProfileBuilder.Build(points);
        var indices = new List<int>();
        DouglasPeuckerSimplify(profile, 0, profile.Length - 1, epsilon, indices);

        // D+/D- on simplified profile
        for (int i = 1; i < indices.Count; i++)
        {
            double delta = profile[indices[i]].Ele - profile[indices[i - 1]].Ele;
            if (delta > 0)
                result.Gain += delta;
            else
                result.Loss += -delta;
        }

        return result;
    }

    private static void DouglasPeuckerSimplify(ProfilePoint[] profile,
        int start, int end, double epsilon, List<int> result)
    {
        if (end - start < 1)
        {
            // Single point or empty range — add all indices
            for (int i = start; i <= end; i++)
            {
                if (result.Count == 0 || result[^1] != i)
                    result.Add(i);
            }
            return;
        }

        // Find point with max perpendicular distance
        double maxDist = 0;
        int maxIdx = start;
        var first = profile[start];
        var last = profile[end];

        for (int i = start + 1; i < end; i++)
        {
            double d = PerpendicularDistance(profile[i], first, last);
            if (d > maxDist)
            {
                maxDist = d;
                maxIdx = i;
            }
        }

        if (maxDist > epsilon)
        {
            // Recursively simplify both halves — no array allocation
            DouglasPeuckerSimplify(profile, start, maxIdx, epsilon, result);
            DouglasPeuckerSimplify(profile, maxIdx, end, epsilon, result);
        }
        else
        {
            // All points within epsilon — keep only endpoints
            if (result.Count == 0 || result[^1] != start)
                result.Add(start);
            result.Add(end);
        }
    }

    private static double PerpendicularDistance(ProfilePoint p, ProfilePoint a, ProfilePoint b)
    {
        if (a.CumDist == b.CumDist)
            return Math.Abs(p.Ele - a.Ele);

        double t = (p.CumDist - a.CumDist) / (b.CumDist - a.CumDist);
        double interpolated = a.Ele + t * (b.Ele - a.Ele);
        return Math.Abs(p.Ele - interpolated);
    }
}

/// <summary>
/// A point on the 1D elevation profile.
/// </summary>
internal readonly record struct ProfilePoint(double CumDist, double Ele);

/// <summary>
/// Builds a (cumulative distance, elevation) profile from track points.
/// </summary>
internal static class ProfileBuilder
{
    public static ProfilePoint[] Build(List<TrackPoint> points)
    {
        var profile = new ProfilePoint[points.Count];
        profile[0] = new ProfilePoint(0, points[0].Ele);

        double cumDist = 0;
        for (int i = 1; i < points.Count; i++)
        {
            double d = DistanceCalculator.Haversine(
                points[i - 1].Lat, points[i - 1].Lon,
                points[i].Lat, points[i].Lon);
            cumDist += d;
            profile[i] = new ProfilePoint(cumDist, points[i].Ele);
        }
        return profile;
    }
}
