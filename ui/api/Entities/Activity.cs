namespace GpxAnalyzer.Api.Entities;

public class Activity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "trail";
    public string? DetectedSubType { get; set; }
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

    // Enrichissement utilisateur
    public string? Description { get; set; }
    public int? PerceivedExertion { get; set; }   // RPE 1-10
    public string? Tags { get; set; }              // JSON array ["tag1","tag2"]
    public string? SessionType { get; set; }

    // Calories (calculé au processing)
    public double? EstimatedCalories { get; set; }
    public string? CalorieMethod { get; set; }     // "hr" | "met"

    // Correction d'anomalies demandée pour le prochain run
    public bool FixAnomaliesOnNextRun { get; set; }
}
