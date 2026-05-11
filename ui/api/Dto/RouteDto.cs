namespace GpxAnalyzer.Api.Dto;

using System.Text.Json.Serialization;

public class RouteListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string RouteCategory { get; set; } = "";
    public string Status { get; set; } = "";
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double EstimatedTimeSeconds { get; set; }
    public string? Tags { get; set; }
    public string RoutingProfile { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RouteDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "";
    public string RouteCategory { get; set; } = "";
    public string Status { get; set; } = "";
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public double MaxElevationM { get; set; }
    public double MinElevationM { get; set; }
    public double EstimatedTimeSeconds { get; set; }
    public string? Tags { get; set; }
    public string RoutingProfile { get; set; } = "";
    public Guid? SourceActivityId { get; set; }
    public string? SourceFileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[][]? Points { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteWaypointDto[]? Waypoints { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoutePoiDto[]? Pois { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Profile { get; set; }
}

public class RouteWaypointDto
{
    public string Id { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int Order { get; set; }
}

public class RoutePoiDto
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? Notes { get; set; }
}

public class RouteCreateDto
{
    public string? Name { get; set; }
    public string ActivityType { get; set; } = "trail";
    public Guid? SourceActivityId { get; set; }
}

public class RouteUpdateDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "trail";
    public string RouteCategory { get; set; } = "";
    public string? Tags { get; set; }
    public string RoutingProfile { get; set; } = "manual";
    public string Status { get; set; } = "draft";
    public double[][]? Points { get; set; }
    public RouteWaypointDto[]? Waypoints { get; set; }
    public RoutePoiDto[]? Pois { get; set; }
}

public class RouteAutoSaveDto
{
    public double[][]? Points { get; set; }
    public RouteWaypointDto[]? Waypoints { get; set; }
    public RoutePoiDto[]? Pois { get; set; }
}
