namespace GpxAnalyzer.Api.Services.Integrations;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

public class StravaService : IActivityImporter
{
    private const string AuthUrl = "https://www.strava.com/oauth/authorize";
    private const string TokenUrl = "https://www.strava.com/oauth/token";
    private const string ApiBase = "https://www.strava.com/api/v3";

    private readonly ISettingsService _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StravaService> _logger;

    public string ProviderName => "strava";

    public StravaService(
        ISettingsService settings,
        IHttpClientFactory httpClientFactory,
        ILogger<StravaService> logger)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAuthorizationUrlAsync(string callbackUrl, string state)
    {
        var clientId = await _settings.GetAsync("Integrations:Strava:ClientId")
            ?? throw new InvalidOperationException("Strava ClientId not configured.");

        return $"{AuthUrl}?client_id={clientId}&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
               $"&scope=read,activity:read_all&approval_prompt=auto" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<TokenInfo> ExchangeCodeAsync(string code, string callbackUrl)
    {
        var clientId = await _settings.GetAsync("Integrations:Strava:ClientId")
            ?? throw new InvalidOperationException("Strava ClientId not configured.");
        var clientSecret = await _settings.GetAsync("Integrations:Strava:ClientSecret")
            ?? throw new InvalidOperationException("Strava ClientSecret not configured.");

        using var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        });

        var response = await client.PostAsync(TokenUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new TokenInfo
        {
            AccessToken = json.GetProperty("access_token").GetString()!,
            RefreshToken = json.GetProperty("refresh_token").GetString(),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(json.GetProperty("expires_at").GetInt64()).UtcDateTime,
            ExternalUserId = json.GetProperty("athlete").GetProperty("id").GetInt64().ToString(),
        };
    }

    public async Task<TokenInfo> RefreshTokenAsync(string refreshToken)
    {
        var clientId = await _settings.GetAsync("Integrations:Strava:ClientId")
            ?? throw new InvalidOperationException("Strava ClientId not configured.");
        var clientSecret = await _settings.GetAsync("Integrations:Strava:ClientSecret")
            ?? throw new InvalidOperationException("Strava ClientSecret not configured.");

        using var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        });

        var response = await client.PostAsync(TokenUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new TokenInfo
        {
            AccessToken = json.GetProperty("access_token").GetString()!,
            RefreshToken = json.GetProperty("refresh_token").GetString(),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(json.GetProperty("expires_at").GetInt64()).UtcDateTime,
        };
    }

    public async Task<bool> ValidateWebhookAsync(HttpContext context)
    {
        // Strava webhook validation: GET with hub.challenge
        if (context.Request.Method == "GET")
        {
            var verifyToken = await _settings.GetAsync("Integrations:Strava:WebhookVerifyToken", "gpx-analyzer") ?? "gpx-analyzer";
            var mode = context.Request.Query["hub.mode"].ToString();
            var token = context.Request.Query["hub.verify_token"].ToString();
            return mode == "subscribe" && token == verifyToken;
        }
        return true;
    }

    public async Task<string?> GetWebhookActivityIdAsync(HttpContext context)
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body);

        var objectType = body.GetProperty("object_type").GetString();
        var aspectType = body.GetProperty("aspect_type").GetString();

        if (objectType == "activity" && aspectType == "create")
        {
            return body.GetProperty("object_id").GetInt64().ToString();
        }

        return null;
    }

    public async Task<ImportedActivity> FetchActivityAsync(string externalId, string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Get activity details
        var activityResponse = await client.GetAsync($"{ApiBase}/activities/{externalId}");
        activityResponse.EnsureSuccessStatusCode();
        var activityJson = await activityResponse.Content.ReadFromJsonAsync<JsonElement>();

        var name = activityJson.GetProperty("name").GetString() ?? "Strava Activity";
        var type = MapStravaType(activityJson.GetProperty("type").GetString() ?? "Run");

        // Get streams (latlng, altitude, time) to reconstruct GPX
        var streamsResponse = await client.GetAsync(
            $"{ApiBase}/activities/{externalId}/streams?keys=latlng,altitude,time&key_by_type=true");
        streamsResponse.EnsureSuccessStatusCode();
        var streamsJson = await streamsResponse.Content.ReadFromJsonAsync<JsonElement>();

        var gpxStream = BuildGpxFromStreams(streamsJson, activityJson);

        return new ImportedActivity
        {
            Name = name,
            ActivityType = type,
            ExternalId = externalId,
            GpxStream = gpxStream,
        };
    }

    private static Stream BuildGpxFromStreams(JsonElement streams, JsonElement activity)
    {
        var latlng = streams.GetProperty("latlng").GetProperty("data");
        var altitude = streams.GetProperty("altitude").GetProperty("data");
        var time = streams.GetProperty("time").GetProperty("data");

        var startDate = activity.GetProperty("start_date").GetString()!;
        var startTime = DateTime.Parse(startDate, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        XNamespace ns = "http://www.topografix.com/GPX/1/1";
        var gpx = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "gpx-analyzer-strava-import"),
                new XElement(ns + "trk",
                    new XElement(ns + "name", activity.GetProperty("name").GetString()),
                    new XElement(ns + "trkseg",
                        Enumerable.Range(0, latlng.GetArrayLength()).Select(i =>
                        {
                            var lat = latlng[i][0].GetDouble();
                            var lon = latlng[i][1].GetDouble();
                            var ele = altitude[i].GetDouble();
                            var seconds = time[i].GetInt32();
                            var pointTime = startTime.AddSeconds(seconds);

                            return new XElement(ns + "trkpt",
                                new XAttribute("lat", lat),
                                new XAttribute("lon", lon),
                                new XElement(ns + "ele", ele),
                                new XElement(ns + "time", pointTime.ToString("o")));
                        })))));

        var memoryStream = new MemoryStream();
        gpx.Save(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static string MapStravaType(string stravaType) => stravaType.ToLowerInvariant() switch
    {
        "run" => "run",
        "trail run" or "trailrun" => "trail",
        "hike" => "hike",
        "ride" or "virtualride" => "cycle",
        "walk" => "walk",
        "swim" => "swim",
        _ => "other",
    };
}
