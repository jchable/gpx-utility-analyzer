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
        var indices = DouglasPeuckerSimplify(profile, epsilon);

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

    private static List<int> DouglasPeuckerSimplify(ProfilePoint[] profile, double epsilon)
    {
        if (profile.Length < 2)
        {
            var all = new List<int>(profile.Length);
            for (int i = 0; i < profile.Length; i++) all.Add(i);
            return all;
        }

        // Find point with max perpendicular distance
        double maxDist = 0;
        int maxIdx = 0;
        var first = profile[0];
        var last = profile[^1];

        for (int i = 1; i < profile.Length - 1; i++)
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
            // Recursively simplify both halves
            var leftProfile = profile[..(maxIdx + 1)];
            var rightProfile = profile[maxIdx..];

            var left = DouglasPeuckerSimplify(leftProfile, epsilon);
            var right = DouglasPeuckerSimplify(rightProfile, epsilon);

            // Combine, avoiding duplicate at split point
            var result = new List<int>(left.Count + right.Count - 1);
            result.AddRange(left);
            for (int i = 1; i < right.Count; i++)
                result.Add(right[i] + maxIdx);
            return result;
        }

        // All points within epsilon — keep only endpoints
        return [0, profile.Length - 1];
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
