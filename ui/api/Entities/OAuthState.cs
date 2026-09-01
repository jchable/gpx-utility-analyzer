namespace GpxAnalyzer.Api.Entities;

public class OAuthState
{
    public string Nonce { get; set; } = "";
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}