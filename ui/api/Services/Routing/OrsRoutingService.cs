using System.Text.Json;

namespace GpxAnalyzer.Api.Services.Routing;

/// <summary>
/// OpenRouteService cloud routing (free tier: 2000 req/day).
/// Supports elevation in responses.
/// </summary>
public class OrsRoutingService : IRoutingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<OrsRoutingService> _logger;

    private static readonly Dictionary<string, string> ProfileMap = new()
    {
        ["hiking"] = "foot-hiking",
        ["trail"] = "foot-hiking",
        ["cycling"] = "cycling-mountain",
        ["road"] = "driving-car",
        ["manual"] = "foot-hiking",
    };

    public OrsRoutingService(HttpClient http, IConfiguration config, ILogger<OrsRoutingService> logger)
    {
        _http = http;
        _apiKey = config["Routing:Ors:ApiKey"] ?? "";
        _baseUrl = config["Routing:Ors:BaseUrl"] ?? "https://api.openrouteservice.org";
        _logger = logger;
    }

    public async Task<RoutingResult> GetRouteAsync(
        List<(double Lat, double Lon)> waypoints,
        string profile,
        CancellationToken ct = default)
    {
        var orsProfile = ProfileMap.GetValueOrDefault(profile, "foot-hiking");

        var body = new
        {
            coordinates = waypoints.Select(w => new[] { w.Lon, w.Lat }).ToArray(),
            elevation = true,
            instructions = false,
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_baseUrl}/v2/directions/{orsProfile}/geojson")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("Authorization", _apiKey);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var result = new RoutingResult();

        // Parse GeoJSON response
        var features = doc.RootElement.GetProperty("features");
        if (features.GetArrayLength() > 0)
        {
            var geometry = features[0].GetProperty("geometry");
            var coords = geometry.GetProperty("coordinates");

            var coordsList = new List<double[]>();
            foreach (var coord in coords.EnumerateArray())
            {
                var arr = coord.EnumerateArray().Select(c => c.GetDouble()).ToArray();
                coordsList.Add(arr); // [lon, lat, ele]
            }

            result.Coordinates = coordsList.ToArray();

            // Extract summary
            var properties = features[0].GetProperty("properties");
            if (properties.TryGetProperty("summary", out var summary))
            {
                if (summary.TryGetProperty("distance", out var dist))
                    result.DistanceMeters = dist.GetDouble();
                if (summary.TryGetProperty("duration", out var dur))
                    result.DurationSeconds = dur.GetDouble();
            }
        }

        _logger.LogInformation("ORS routing: {Profile}, {Points} waypoints → {Coords} coords, {Dist:F0}m",
            orsProfile, waypoints.Count, result.Coordinates.Length, result.DistanceMeters);

        return result;
    }
}
