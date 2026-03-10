namespace GpxAnalyzer.Api.Dto;

using System.Text.Json.Serialization;

public class ActivityListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public DateTime StartTime { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double MovingTimeSeconds { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "";
}

public class ActivityDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public double MovingTimeSeconds { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Stats { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? AiReport { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateActivityDto
{
    public string? ActivityType { get; set; }
    public string? Name { get; set; }
}

public class DashboardSummaryDto
{
    public int TotalActivities { get; set; }
    public double TotalDistanceKm { get; set; }
    public double TotalElevationGainM { get; set; }
    public double TotalMovingTimeSeconds { get; set; }
    public int ActivitiesThisMonth { get; set; }
    public double DistanceThisMonthKm { get; set; }
    public double ElevationGainThisMonthM { get; set; }
    public double MovingTimeThisMonthSeconds { get; set; }
    public List<ActivityListDto> RecentActivities { get; set; } = [];
    public Dictionary<string, int> ActivityTypeBreakdown { get; set; } = [];
}

public class IntegrationDto
{
    public string Provider { get; set; } = "";
    public bool IsConnected { get; set; }
    public string? ExternalUserId { get; set; }
    public DateTime? ConnectedAt { get; set; }
}
