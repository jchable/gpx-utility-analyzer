namespace GpxAnalyzer.Api.Services;

using System.Text.Json;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;
using Microsoft.EntityFrameworkCore;

public class RouteService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RouteService> _logger;

    public RouteService(AppDbContext db, ILogger<RouteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<RouteListDto>> ListAsync(Guid userId, int page, int pageSize, string? type, string? status, CancellationToken ct = default)
    {
        var query = _db.Routes.Where(r => r.UserId == userId);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.ActivityType == type);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        return await query
            .OrderByDescending(r => r.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RouteListDto
            {
                Id = r.Id,
                Name = r.Name,
                ActivityType = r.ActivityType,
                RouteCategory = r.RouteCategory,
                Status = r.Status,
                DistanceKm = r.DistanceKm,
                ElevationGainM = r.ElevationGainM,
                EstimatedTimeSeconds = r.EstimatedTimeSeconds,
                Tags = r.Tags,
                RoutingProfile = r.RoutingProfile,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<RouteDetailDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return null;

        return MapToDetail(route);
    }

    public async Task<Entities.Route> CreateAsync(Guid userId, RouteCreateDto dto, string language, CancellationToken ct = default)
    {
        var route = new Entities.Route
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name ?? "New route",
            ActivityType = dto.ActivityType,
            SourceActivityId = dto.SourceActivityId,
            Language = language,
        };

        _db.Routes.Add(route);
        await _db.SaveChangesAsync(ct);

        // The route id is enough to find the row; its name and source filename are user
        // text and must not reach a log line verbatim (CodeQL cs/log-forging).
        _logger.LogInformation("Route created: {Id}", route.Id);
        return route;
    }

    public async Task<Entities.Route?> UpdateAsync(Guid userId, Guid id, RouteUpdateDto dto, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return null;

        route.Name = dto.Name;
        route.Description = dto.Description;
        route.ActivityType = dto.ActivityType;
        route.RouteCategory = dto.RouteCategory;
        route.Tags = dto.Tags;
        route.RoutingProfile = dto.RoutingProfile;
        route.Status = dto.Status;
        route.UpdatedAt = DateTime.UtcNow;

        if (dto.Points is not null)
            route.PointsJson = JsonSerializer.Serialize(dto.Points);

        if (dto.Waypoints is not null)
            route.WaypointsJson = JsonSerializer.Serialize(dto.Waypoints);

        if (dto.Pois is not null)
            route.PoisJson = JsonSerializer.Serialize(dto.Pois);

        // Recompute stats from points
        if (dto.Points is { Length: >= 2 })
        {
            ComputeStats(route, dto.Points);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Route updated: {Id}, {Dist:F1} km", route.Id, route.DistanceKm);
        return route;
    }

    public async Task<bool> AutoSaveAsync(Guid userId, Guid id, RouteAutoSaveDto dto, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return false;

        if (dto.Points is not null)
            route.PointsJson = JsonSerializer.Serialize(dto.Points);

        if (dto.Waypoints is not null)
            route.WaypointsJson = JsonSerializer.Serialize(dto.Waypoints);

        if (dto.Pois is not null)
            route.PoisJson = JsonSerializer.Serialize(dto.Pois);

        route.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return false;

        _db.Routes.Remove(route);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Route deleted: {Id}", id);
        return true;
    }

    public async Task<Entities.Route?> CreateFromActivityAsync(Guid userId, Guid activityId, string language, CancellationToken ct = default)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == activityId && a.UserId == userId, ct);
        if (activity is null) return null;

        var route = new Entities.Route
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = $"{activity.Name} (route)",
            ActivityType = activity.ActivityType,
            SourceActivityId = activityId,
            Language = language,
            DistanceKm = activity.DistanceKm,
            ElevationGainM = activity.ElevationGainM,
            ElevationLossM = activity.ElevationLossM,
        };

        // Copy track coordinates from activity
        if (activity.TrackGeoJson is not null)
        {
            var geoJson = JsonSerializer.Deserialize<JsonElement>(activity.TrackGeoJson);
            if (geoJson.TryGetProperty("coordinates", out var coords))
            {
                route.PointsJson = coords.GetRawText();
            }
        }

        _db.Routes.Add(route);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Route created from activity {ActivityId}: {RouteId}", activityId, route.Id);
        return route;
    }

    public async Task<Entities.Route?> ImportGpxAsync(Guid userId, Stream gpxStream, string filename, string language, CancellationToken ct = default)
    {
        // Save to temp file for parsing
        var tempFile = Path.Combine(Path.GetTempPath(), $"gpx-import-{Guid.NewGuid()}.gpx");
        try
        {
            using (var fs = File.Create(tempFile))
            {
                await gpxStream.CopyToAsync(fs, ct);
            }

            var doc = GpxParser.ParseFile(tempFile);
            var points = doc.AllPoints();

            if (points.Count == 0) return null;

            // Extract coordinates [lon, lat, ele]
            var coords = points.Select(p => new[] { p.Lon, p.Lat, p.Ele }).ToArray();

            var route = new Entities.Route
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = Path.GetFileNameWithoutExtension(filename),
                SourceFileName = filename,
                Language = language,
                PointsJson = JsonSerializer.Serialize(coords),
            };

            ComputeStats(route, coords);

            _db.Routes.Add(route);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Route imported from GPX: {Id}, {Points} points, {Dist:F1} km",
                route.Id, points.Count, route.DistanceKm);

            return route;
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    public async Task<List<string>> GetTagsAsync(Guid userId, CancellationToken ct = default)
    {
        var routes = await _db.Routes
            .Where(r => r.UserId == userId && r.Tags != null && r.Tags != "")
            .Select(r => r.Tags!)
            .ToListAsync(ct);

        return routes
            .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList();
    }

    public record ExportResult(MemoryStream Stream, string FileName);

    public async Task<ExportResult?> ExportGpxAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return null;

        var coords = route.PointsJson is not null
            ? JsonSerializer.Deserialize<double[][]>(route.PointsJson) ?? []
            : [];

        List<(string Name, double Lon, double Lat, string? Type)>? pois = null;
        if (route.PoisJson is not null)
        {
            var poiDtos = JsonSerializer.Deserialize<RoutePoiDto[]>(route.PoisJson);
            pois = poiDtos?.Select(p => (p.Name, p.Lon, p.Lat, (string?)p.Type)).ToList();
        }

        var ms = new MemoryStream();
        GpxWriter.WriteRoute(ms, route.Name, coords, pois);
        ms.Position = 0;

        var fileName = SanitizeFileName(route.Name) + ".gpx";
        return new ExportResult(ms, fileName);
    }

    public async Task<ExportResult?> ExportGeoJsonAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return null;

        var coords = route.PointsJson is not null
            ? JsonSerializer.Deserialize<double[][]>(route.PointsJson) ?? []
            : [];

        var poiDtos = route.PoisJson is not null
            ? JsonSerializer.Deserialize<RoutePoiDto[]>(route.PoisJson) ?? []
            : [];

        var features = new List<object>();

        // Track as LineString
        if (coords.Length >= 2)
        {
            features.Add(new
            {
                type = "Feature",
                properties = new { name = route.Name, activityType = route.ActivityType },
                geometry = new { type = "LineString", coordinates = coords }
            });
        }

        // POIs as Points
        foreach (var poi in poiDtos)
        {
            features.Add(new
            {
                type = "Feature",
                properties = new { name = poi.Name, poiType = poi.Type, notes = poi.Notes },
                geometry = new { type = "Point", coordinates = new[] { poi.Lon, poi.Lat } }
            });
        }

        var geoJson = new { type = "FeatureCollection", features };
        var json = JsonSerializer.Serialize(geoJson, new JsonSerializerOptions { WriteIndented = true });

        var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, leaveOpen: true);
        await writer.WriteAsync(json);
        await writer.FlushAsync(ct);
        ms.Position = 0;

        var fileName = SanitizeFileName(route.Name) + ".geojson";
        return new ExportResult(ms, fileName);
    }

    public async Task<ExportResult?> ExportKmlAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (route is null) return null;

        var coords = route.PointsJson is not null
            ? JsonSerializer.Deserialize<double[][]>(route.PointsJson) ?? []
            : [];

        List<(string Name, double Lon, double Lat, string? Description)>? pois = null;
        if (route.PoisJson is not null)
        {
            var poiDtos = JsonSerializer.Deserialize<RoutePoiDto[]>(route.PoisJson);
            pois = poiDtos?.Select(p => (p.Name, p.Lon, p.Lat, p.Notes)).ToList();
        }

        var ms = new MemoryStream();
        KmlWriter.Write(ms, route.Name, coords, pois);
        ms.Position = 0;

        var fileName = SanitizeFileName(route.Name) + ".kml";
        return new ExportResult(ms, fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "route" : sanitized;
    }

    private static void ComputeStats(Entities.Route route, double[][] coords)
    {
        double totalDistance = 0;
        double elevGain = 0, elevLoss = 0;
        double maxEle = double.MinValue, minEle = double.MaxValue;

        for (int i = 0; i < coords.Length; i++)
        {
            double ele = coords[i].Length > 2 ? coords[i][2] : 0;
            if (ele > maxEle) maxEle = ele;
            if (ele < minEle) minEle = ele;

            if (i > 0)
            {
                totalDistance += DistanceCalculator.Haversine(
                    coords[i - 1][1], coords[i - 1][0],
                    coords[i][1], coords[i][0]);

                double prevEle = coords[i - 1].Length > 2 ? coords[i - 1][2] : 0;
                double dEle = ele - prevEle;
                if (dEle > 2.0) elevGain += dEle;
                else if (dEle < -2.0) elevLoss += Math.Abs(dEle);
            }
        }

        route.DistanceKm = totalDistance / 1000.0;
        route.ElevationGainM = elevGain;
        route.ElevationLossM = elevLoss;
        route.MaxElevationM = maxEle == double.MinValue ? 0 : maxEle;
        route.MinElevationM = minEle == double.MaxValue ? 0 : minEle;

        // Tobler estimated time
        route.EstimatedTimeSeconds = ComputeToblerTime(coords);
    }

    private static double ComputeToblerTime(double[][] coords)
    {
        double totalSeconds = 0;
        for (int i = 1; i < coords.Length; i++)
        {
            double dist = DistanceCalculator.Haversine(
                coords[i - 1][1], coords[i - 1][0],
                coords[i][1], coords[i][0]);

            if (dist < 0.1) continue;

            double prevEle = coords[i - 1].Length > 2 ? coords[i - 1][2] : 0;
            double ele = coords[i].Length > 2 ? coords[i][2] : 0;
            double slope = (ele - prevEle) / dist;

            // Tobler's hiking function: v = 6 * exp(-3.5 * |slope + 0.05|) km/h
            double speed = 6.0 * Math.Exp(-3.5 * Math.Abs(slope + 0.05));
            speed = Math.Max(speed, 0.5); // min 0.5 km/h

            double speedMs = speed * 1000.0 / 3600.0;
            totalSeconds += dist / speedMs;
        }

        return totalSeconds;
    }

    private static RouteDetailDto MapToDetail(Entities.Route route)
    {
        var dto = new RouteDetailDto
        {
            Id = route.Id,
            Name = route.Name,
            Description = route.Description,
            ActivityType = route.ActivityType,
            RouteCategory = route.RouteCategory,
            Status = route.Status,
            DistanceKm = route.DistanceKm,
            ElevationGainM = route.ElevationGainM,
            ElevationLossM = route.ElevationLossM,
            MaxElevationM = route.MaxElevationM,
            MinElevationM = route.MinElevationM,
            EstimatedTimeSeconds = route.EstimatedTimeSeconds,
            Tags = route.Tags,
            RoutingProfile = route.RoutingProfile,
            SourceActivityId = route.SourceActivityId,
            SourceFileName = route.SourceFileName,
            CreatedAt = route.CreatedAt,
            UpdatedAt = route.UpdatedAt,
        };

        if (route.PointsJson is not null)
            dto.Points = JsonSerializer.Deserialize<double[][]>(route.PointsJson);

        if (route.WaypointsJson is not null)
            dto.Waypoints = JsonSerializer.Deserialize<RouteWaypointDto[]>(route.WaypointsJson);

        if (route.PoisJson is not null)
            dto.Pois = JsonSerializer.Deserialize<RoutePoiDto[]>(route.PoisJson);

        if (route.ProfileJson is not null)
            dto.Profile = JsonSerializer.Deserialize<object>(route.ProfileJson);

        return dto;
    }
}
