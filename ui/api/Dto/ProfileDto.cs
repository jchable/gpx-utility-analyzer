namespace GpxAnalyzer.Api.Dto;

public class UserProfileDto
{
    // User info
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string PreferredUnits { get; set; } = "metric";
    public string Language { get; set; } = "en";
    public string? ProfilePhotoPath { get; set; }

    // AthleteProfile
    public double? WeightKg { get; set; }
    public double? HeightCm { get; set; }
    public string? Sex { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int? MaxHeartRate { get; set; }
    public int? RestingHeartRate { get; set; }
    public int? Ftp { get; set; }
    public double? Vo2Max { get; set; }

    // Computed
    public int? Age { get; set; }
    public int? EstimatedMaxHR { get; set; }
    public double? Bmi { get; set; }
}

public class UpdateProfileDto
{
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string? PreferredUnits { get; set; }
    public string? Language { get; set; }

    public double? WeightKg { get; set; }
    public double? HeightCm { get; set; }
    public string? Sex { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int? MaxHeartRate { get; set; }
    public int? RestingHeartRate { get; set; }
    public int? Ftp { get; set; }
    public double? Vo2Max { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
