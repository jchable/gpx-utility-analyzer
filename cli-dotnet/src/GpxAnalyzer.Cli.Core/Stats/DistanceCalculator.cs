namespace GpxAnalyzer.Cli.Core.Stats;

public static class DistanceCalculator
{
    private const double EarthRadius = 6371000; // meters
    private const double DegToRad = Math.PI / 180.0;

    /// <summary>
    /// Computes great-circle distance in meters between two points (Haversine formula).
    /// </summary>
    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double lat1Rad = lat1 * DegToRad;
        double lat2Rad = lat2 * DegToRad;
        double dLat = (lat2 - lat1) * DegToRad;
        double dLon = (lon2 - lon1) * DegToRad;

        double sinDLat2 = Math.Sin(dLat * 0.5);
        double sinDLon2 = Math.Sin(dLon * 0.5);
        double a = sinDLat2 * sinDLat2 +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   sinDLon2 * sinDLon2;
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
}
