namespace GpxAnalyzer.Api.Services.Integrations;

using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

public class GarminService : IActivityImporter
{
    private const string RequestTokenUrl = "https://connectapi.garmin.com/oauth-service/oauth/request_token";
    private const string AccessTokenUrl = "https://connectapi.garmin.com/oauth-service/oauth/access_token";
    private const string AuthorizeUrl = "https://connect.garmin.com/oauthConfirm";
    private const string ApiBase = "https://apis.garmin.com";

    private readonly ISettingsService _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GarminService> _logger;

    public string ProviderName => "garmin";

    public GarminService(
        ISettingsService settings,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<GarminService> logger)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetAuthorizationUrlAsync(string callbackUrl)
    {
        var consumerKey = await _settings.GetAsync("Integrations:Garmin:ConsumerKey")
            ?? throw new InvalidOperationException("Garmin ConsumerKey not configured.");
        var consumerSecret = await _settings.GetAsync("Integrations:Garmin:ConsumerSecret")
            ?? throw new InvalidOperationException("Garmin ConsumerSecret not configured.");

        using var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, RequestTokenUrl);

        OAuth1Helper.SignRequest(request, consumerKey, consumerSecret,
            extraParams: new Dictionary<string, string>
            {
                ["oauth_callback"] = callbackUrl,
            });

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var parsed = ParseFormEncoded(body);

        var oauthToken = parsed["oauth_token"];
        var oauthTokenSecret = parsed["oauth_token_secret"];

        // Store the token secret temporarily for the callback
        _cache.Set($"garmin:request_token:{oauthToken}", oauthTokenSecret, TimeSpan.FromMinutes(10));

        return $"{AuthorizeUrl}?oauth_token={Uri.EscapeDataString(oauthToken)}";
    }

    public async Task<TokenInfo> ExchangeCodeAsync(string code, string callbackUrl)
    {
        // code is a compound string: "oauth_token|oauth_verifier"
        var parts = code.Split('|');
        if (parts.Length != 2)
            throw new InvalidOperationException("Invalid Garmin OAuth callback data.");

        var oauthToken = parts[0];
        var oauthVerifier = parts[1];

        var tokenSecret = _cache.Get<string>($"garmin:request_token:{oauthToken}")
            ?? throw new InvalidOperationException("Garmin OAuth request token expired.");

        var consumerKey = await _settings.GetAsync("Integrations:Garmin:ConsumerKey")
            ?? throw new InvalidOperationException("Garmin ConsumerKey not configured.");
        var consumerSecret = await _settings.GetAsync("Integrations:Garmin:ConsumerSecret")
            ?? throw new InvalidOperationException("Garmin ConsumerSecret not configured.");

        using var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl);

        OAuth1Helper.SignRequest(request, consumerKey, consumerSecret, oauthToken, tokenSecret,
            extraParams: new Dictionary<string, string>
            {
                ["oauth_verifier"] = oauthVerifier,
            });

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var parsed = ParseFormEncoded(body);

        var accessToken = parsed["oauth_token"];
        var accessTokenSecret = parsed["oauth_token_secret"];

        _cache.Remove($"garmin:request_token:{oauthToken}");

        return new TokenInfo
        {
            // Store both token and secret as compound string
            AccessToken = $"{accessToken}|{accessTokenSecret}",
            ExpiresAt = null, // Garmin tokens don't expire
        };
    }

    public Task<TokenInfo> RefreshTokenAsync(string refreshToken)
    {
        // Garmin tokens are permanent, no refresh needed
        throw new NotSupportedException("Garmin tokens do not expire.");
    }

    public Task<bool> ValidateWebhookAsync(HttpContext context)
    {
        // Garmin webhook validation is typically done during registration
        return Task.FromResult(true);
    }

    public async Task<string?> GetWebhookActivityIdAsync(HttpContext context)
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body);

        // Garmin webhook payload contains activityDetails array
        if (body.TryGetProperty("activityDetails", out var details) && details.GetArrayLength() > 0)
        {
            var activityId = details[0].GetProperty("activityId").GetInt64();
            return activityId.ToString();
        }

        return null;
    }

    public async Task<ImportedActivity> FetchActivityAsync(string externalId, string accessToken)
    {
        var (token, tokenSecret) = SplitToken(accessToken);
        var consumerKey = await _settings.GetAsync("Integrations:Garmin:ConsumerKey")!;
        var consumerSecret = await _settings.GetAsync("Integrations:Garmin:ConsumerSecret")!;

        using var client = _httpClientFactory.CreateClient();

        // Try GPX download first
        Stream? gpxStream = null;
        try
        {
            gpxStream = await DownloadGpxAsync(client, externalId, consumerKey!, consumerSecret!, token, tokenSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPX download failed for Garmin activity {Id}, falling back to FIT", externalId);
        }

        // Fall back to FIT download + conversion
        if (gpxStream is null)
        {
            gpxStream = await DownloadFitAsGpxAsync(client, externalId, consumerKey!, consumerSecret!, token, tokenSecret);
        }

        // Fetch activity metadata for name and type
        var (name, activityType) = await FetchActivityMetadataAsync(
            client, externalId, consumerKey!, consumerSecret!, token, tokenSecret);

        return new ImportedActivity
        {
            Name = name,
            ActivityType = activityType,
            ExternalId = externalId,
            GpxStream = gpxStream,
        };
    }

    private async Task<Stream> DownloadGpxAsync(
        HttpClient client, string activityId,
        string consumerKey, string consumerSecret,
        string token, string tokenSecret)
    {
        var url = $"{ApiBase}/wellness-api/rest/activityFile?id={activityId}&fileType=GPX";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        OAuth1Helper.SignRequest(request, consumerKey, consumerSecret, token, tokenSecret);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    private async Task<Stream> DownloadFitAsGpxAsync(
        HttpClient client, string activityId,
        string consumerKey, string consumerSecret,
        string token, string tokenSecret)
    {
        var url = $"{ApiBase}/wellness-api/rest/activityFile?id={activityId}&fileType=FIT";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        OAuth1Helper.SignRequest(request, consumerKey, consumerSecret, token, tokenSecret);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var fitStream = await response.Content.ReadAsStreamAsync();
        return FitToGpxConverter.Convert(fitStream);
    }

    private async Task<(string Name, string Type)> FetchActivityMetadataAsync(
        HttpClient client, string activityId,
        string consumerKey, string consumerSecret,
        string token, string tokenSecret)
    {
        try
        {
            var url = $"{ApiBase}/wellness-api/rest/activityDetails?activityId={activityId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            OAuth1Helper.SignRequest(request, consumerKey, consumerSecret, token, tokenSecret);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            var name = json.TryGetProperty("activityName", out var nameEl)
                ? nameEl.GetString() ?? "Garmin Activity"
                : "Garmin Activity";

            var garminType = json.TryGetProperty("activityType", out var typeEl)
                ? typeEl.GetString() ?? ""
                : "";

            return (name, MapGarminType(garminType));
        }
        catch (Exception)
        {
            return ("Garmin Activity", "other");
        }
    }

    private static (string Token, string Secret) SplitToken(string compoundToken)
    {
        var parts = compoundToken.Split('|');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : throw new InvalidOperationException("Invalid Garmin access token format.");
    }

    private static Dictionary<string, string> ParseFormEncoded(string body)
    {
        return body.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(
                p => Uri.UnescapeDataString(p[0]),
                p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");
    }

    private static string MapGarminType(string garminType) => garminType.ToLowerInvariant() switch
    {
        "running" => "run",
        "trail_running" => "trail",
        "hiking" => "hike",
        "cycling" or "road_biking" or "mountain_biking" or "gravel_cycling" => "cycle",
        "walking" => "walk",
        "lap_swimming" or "open_water_swimming" => "swim",
        _ => "other",
    };
}
