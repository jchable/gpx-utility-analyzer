namespace GpxAnalyzer.Api.Entities;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<Route> Routes { get; set; } = [];
    public ICollection<Integration> Integrations { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
