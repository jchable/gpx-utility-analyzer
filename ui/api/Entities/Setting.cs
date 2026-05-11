namespace GpxAnalyzer.Api.Entities;

public class Setting
{
    public string Key { get; set; } = "";
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Value { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
