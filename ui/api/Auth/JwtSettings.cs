namespace GpxAnalyzer.Api.Auth;

public class JwtSettings
{
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "gpx-analyzer";
    public string Audience { get; set; } = "gpx-analyzer-client";
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
