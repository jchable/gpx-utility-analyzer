namespace GpxAnalyzer.Api.Entities;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string? ProfilePhotoPath { get; set; }
    public string PreferredUnits { get; set; } = "metric";
    public string Language { get; set; } = "en";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public AthleteProfile? AthleteProfile { get; set; }
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<Route> Routes { get; set; } = [];
    public ICollection<Integration> Integrations { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
