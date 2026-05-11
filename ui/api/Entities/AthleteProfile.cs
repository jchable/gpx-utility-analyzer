namespace GpxAnalyzer.Api.Entities;

public class AthleteProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Biometrics
    public double? WeightKg { get; set; }
    public double? HeightCm { get; set; }
    public string? Sex { get; set; }           // "male" | "female" | "other"
    public DateTime? DateOfBirth { get; set; }

    // Performance
    public int? MaxHeartRate { get; set; }
    public int? RestingHeartRate { get; set; }
    public int? Ftp { get; set; }              // watts
    public double? Vo2Max { get; set; }        // mL/kg/min

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;

    // Computed (not persisted)
    public int? Age => DateOfBirth.HasValue
        ? (int)((DateTime.UtcNow - DateOfBirth.Value).TotalDays / 365.25)
        : null;

    public int EstimatedMaxHR => MaxHeartRate
        ?? (Age.HasValue ? 220 - Age.Value : 185);

    public double? Bmi => (WeightKg.HasValue && HeightCm.HasValue && HeightCm > 0)
        ? WeightKg.Value / Math.Pow(HeightCm.Value / 100.0, 2)
        : null;
}
