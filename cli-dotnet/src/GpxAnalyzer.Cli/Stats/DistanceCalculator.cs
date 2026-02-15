namespace GpxAnalyzer.Cli.Stats;

public static class DistanceCalculator
{
    private const double EarthRadius = 6371000; // meters

    /// <summary>
    /// Computes great-circle distance in meters between two points (Haversine formula).
    /// </summary>
    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadius * c;
    }

    /// <summary>
    /// Computes 3D distance accounting for elevation change.
    /// </summary>
    public static double Distance3D(double lat1, double lon1, double ele1,
                                     double lat2, double lon2, double ele2)
    {
        double d2d = Haversine(lat1, lon1, lat2, lon2);
        double dEle = ele2 - ele1;
        return Math.Sqrt(d2d * d2d + dEle * dEle);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}
