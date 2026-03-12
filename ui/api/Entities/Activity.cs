namespace GpxAnalyzer.Api.Entities;

public class Activity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "trail";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public double MovingTimeSeconds { get; set; }
    public string GpxFilePath { get; set; } = "";
    public string? StatsJson { get; set; }
    public string? AiReportJson { get; set; }
    public string? ProfileJson { get; set; }
    public string? TrackGeoJson { get; set; }
    public string? SplitsJson { get; set; }
    public string Source { get; set; } = "upload";
    public string? ExternalId { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string Language { get; set; } = "en";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
