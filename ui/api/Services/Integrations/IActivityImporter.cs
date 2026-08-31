namespace GpxAnalyzer.Api.Services.Integrations;

public interface IActivityImporter
{
    string ProviderName { get; }
    Task<string> GetAuthorizationUrlAsync(string callbackUrl, string state);
    Task<TokenInfo> ExchangeCodeAsync(string code, string callbackUrl);
    Task<TokenInfo> RefreshTokenAsync(string refreshToken);
    Task<bool> ValidateWebhookAsync(HttpContext context);
    Task<string?> GetWebhookActivityIdAsync(HttpContext context);
    Task<ImportedActivity> FetchActivityAsync(string externalId, string accessToken);
}

public class TokenInfo
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ExternalUserId { get; set; }
}

public class ImportedActivity
{
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public Stream GpxStream { get; set; } = Stream.Null;
}
