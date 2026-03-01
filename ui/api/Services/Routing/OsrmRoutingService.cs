using System.Text.Json;

namespace GpxAnalyzer.Api.Services.Routing;

/// <summary>
/// OSRM self-hosted routing. Does NOT return elevation — DEM lookup needed after.
/// </summary>
public class OsrmRoutingService : IRoutingService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly ILogger<OsrmRoutingService> _logger;

    private static readonly Dictionary<string, string> ProfileMap = new()
    {
        ["hiking"] = "foot",
        ["trail"] = "foot",
        ["cycling"] = "bike",
        ["road"] = "car",
        ["manual"] = "foot",
    };

    public OsrmRoutingService(HttpClient http, IConfiguration config, ILogger<OsrmRoutingService> logger)
    {
        _http = http;
        _baseUrl = config["Routing:Osrm:BaseUrl"] ?? "http://osrm:5000";
        _logger = logger;
    }

    public async Task<RoutingResult> GetRouteAsync(
        List<(double Lat, double Lon)> waypoints,
        string profile,
        CancellationToken ct = default)
    {
        var osrmProfile = ProfileMap.GetValueOrDefault(profile, "foot");
        var coordsStr = string.Join(";", waypoints.Select(w =>
            $"{w.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{w.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

        var url = $"{_baseUrl}/route/v1/{osrmProfile}/{coordsStr}?overview=full&geometries=geojson";

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var result = new RoutingResult();

        var routes = doc.RootElement.GetProperty("routes");
        if (routes.GetArrayLength() > 0)
        {
            var route = routes[0];

            var geometry = route.GetProperty("geometry");
            var coords = geometry.GetProperty("coordinates");

            var coordsList = new List<double[]>();
            foreach (var coord in coords.EnumerateArray())
            {
                var arr = coord.EnumerateArray().Select(c => c.GetDouble()).ToArray();
                coordsList.Add(arr); // [lon, lat] — no elevation from OSRM
            }

            result.Coordinates = coordsList.ToArray();

            if (route.TryGetProperty("distance", out var dist))
                result.DistanceMeters = dist.GetDouble();
            if (route.TryGetProperty("duration", out var dur))
                result.DurationSeconds = dur.GetDouble();
        }

        _logger.LogInformation("OSRM routing: {Profile}, {Points} waypoints → {Coords} coords, {Dist:F0}m",
            osrmProfile, waypoints.Count, result.Coordinates.Length, result.DistanceMeters);

        return result;
    }
}
