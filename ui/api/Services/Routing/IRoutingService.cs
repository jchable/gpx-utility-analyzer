namespace GpxAnalyzer.Api.Services.Routing;

public interface IRoutingService
{
    Task<RoutingResult> GetRouteAsync(
        List<(double Lat, double Lon)> waypoints,
        string profile,
        CancellationToken ct = default);
}

public class RoutingResult
{
    public double[][] Coordinates { get; set; } = [];
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
}
