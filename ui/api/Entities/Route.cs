namespace GpxAnalyzer.Api.Entities;

public class Route
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "trail";
    public string RouteCategory { get; set; } = "";
    public string Status { get; set; } = "draft";
    public string? PointsJson { get; set; }
    public string? WaypointsJson { get; set; }
    public string? PoisJson { get; set; }
    public string? ProfileJson { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public double MaxElevationM { get; set; }
    public double MinElevationM { get; set; }
    public double EstimatedTimeSeconds { get; set; }
    public string? Tags { get; set; }
    public string RoutingProfile { get; set; } = "manual";
    public Guid? SourceActivityId { get; set; }
    public string? SourceFileName { get; set; }
    public string Language { get; set; } = "en";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
