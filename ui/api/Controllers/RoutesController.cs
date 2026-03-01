namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Routing;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly RouteService _routeService;
    private readonly RouteElevationService _elevationService;
    private readonly IRoutingService? _routingService;

    public RoutesController(RouteService routeService, RouteElevationService elevationService, IRoutingService? routingService = null)
    {
        _routeService = routeService;
        _elevationService = elevationService;
        _routingService = routingService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RouteListDto>>> GetRoutes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var routes = await _routeService.ListAsync(page, pageSize, type, status, ct);
        return routes;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RouteDetailDto>> GetRoute(Guid id, CancellationToken ct = default)
    {
        var route = await _routeService.GetAsync(id, ct);
        if (route is null) return NotFound();
        return route;
    }

    [HttpPost]
    public async Task<ActionResult<RouteDetailDto>> CreateRoute([FromBody] RouteCreateDto dto, CancellationToken ct = default)
    {
        var language = GetLanguage();
        var route = await _routeService.CreateAsync(dto, language, ct);
        var detail = await _routeService.GetAsync(route.Id, ct);
        return CreatedAtAction(nameof(GetRoute), new { id = route.Id }, detail);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RouteDetailDto>> UpdateRoute(Guid id, [FromBody] RouteUpdateDto dto, CancellationToken ct = default)
    {
        // Enrich with DEM elevation at save time
        if (dto.Points is { Length: >= 2 })
        {
            dto.Points = await _elevationService.EnrichElevationAsync(dto.Points, ct);
        }

        var route = await _routeService.UpdateAsync(id, dto, ct);
        if (route is null) return NotFound();

        var detail = await _routeService.GetAsync(id, ct);
        return Ok(detail);
    }

    [HttpPatch("{id:guid}/autosave")]
    public async Task<IActionResult> AutoSave(Guid id, [FromBody] RouteAutoSaveDto dto, CancellationToken ct = default)
    {
        var success = await _routeService.AutoSaveAsync(id, dto, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRoute(Guid id, CancellationToken ct = default)
    {
        var success = await _routeService.DeleteAsync(id, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("from-activity/{activityId:guid}")]
    public async Task<ActionResult<RouteDetailDto>> CreateFromActivity(Guid activityId, CancellationToken ct = default)
    {
        var language = GetLanguage();
        var route = await _routeService.CreateFromActivityAsync(activityId, language, ct);
        if (route is null) return NotFound();

        var detail = await _routeService.GetAsync(route.Id, ct);
        return CreatedAtAction(nameof(GetRoute), new { id = route.Id }, detail);
    }

    [HttpPost("import")]
    public async Task<ActionResult<RouteDetailDto>> ImportGpx(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "NO_FILE_PROVIDED" });

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { code = "INVALID_FILE_TYPE" });

        var language = GetLanguage();
        using var stream = file.OpenReadStream();
        var route = await _routeService.ImportGpxAsync(stream, file.FileName, language, ct);
        if (route is null) return BadRequest(new { code = "EMPTY_GPX_FILE" });

        var detail = await _routeService.GetAsync(route.Id, ct);
        return CreatedAtAction(nameof(GetRoute), new { id = route.Id }, detail);
    }

    [HttpGet("tags")]
    public async Task<ActionResult<List<string>>> GetTags(CancellationToken ct = default)
    {
        var tags = await _routeService.GetTagsAsync(ct);
        return tags;
    }

    // --- Export endpoints ---

    [HttpGet("{id:guid}/export/gpx")]
    public async Task<IActionResult> ExportGpx(Guid id, CancellationToken ct = default)
    {
        var data = await _routeService.ExportGpxAsync(id, ct);
        if (data is null) return NotFound();
        return File(data.Stream, "application/gpx+xml", data.FileName);
    }

    [HttpGet("{id:guid}/export/geojson")]
    public async Task<IActionResult> ExportGeoJson(Guid id, CancellationToken ct = default)
    {
        var data = await _routeService.ExportGeoJsonAsync(id, ct);
        if (data is null) return NotFound();
        return File(data.Stream, "application/geo+json", data.FileName);
    }

    [HttpGet("{id:guid}/export/kml")]
    public async Task<IActionResult> ExportKml(Guid id, CancellationToken ct = default)
    {
        var data = await _routeService.ExportKmlAsync(id, ct);
        if (data is null) return NotFound();
        return File(data.Stream, "application/vnd.google-earth.kml+xml", data.FileName);
    }

    // --- Routing preview ---

    [HttpPost("routing/preview")]
    public async Task<ActionResult<RoutingPreviewResult>> RoutingPreview([FromBody] RoutingPreviewRequest request, CancellationToken ct = default)
    {
        if (_routingService is null)
            return BadRequest(new { code = "ROUTING_NOT_CONFIGURED" });

        if (request.Waypoints is null || request.Waypoints.Length < 2)
            return BadRequest(new { code = "INSUFFICIENT_WAYPOINTS" });

        var waypoints = request.Waypoints
            .Select(w => (w[0], w[1])) // [lat, lon]
            .ToList();

        var result = await _routingService.GetRouteAsync(waypoints, request.Profile ?? "hiking", ct);

        return Ok(new RoutingPreviewResult
        {
            Coordinates = result.Coordinates,
            DistanceMeters = result.DistanceMeters,
            DurationSeconds = result.DurationSeconds,
        });
    }

    // --- Elevation enrichment (on-demand button) ---

    [HttpPost("{id:guid}/elevation")]
    public async Task<ActionResult<RouteDetailDto>> EnrichElevation(Guid id, CancellationToken ct = default)
    {
        var detail = await _routeService.GetAsync(id, ct);
        if (detail is null) return NotFound();

        if (detail.Points is not { Length: >= 2 })
            return Ok(detail);

        var enriched = await _elevationService.EnrichElevationAsync(detail.Points, ct);

        // Update route with enriched points
        var updateDto = new RouteUpdateDto
        {
            Name = detail.Name,
            Description = detail.Description,
            ActivityType = detail.ActivityType,
            RouteCategory = detail.RouteCategory,
            Tags = detail.Tags,
            RoutingProfile = detail.RoutingProfile,
            Status = detail.Status,
            Points = enriched,
        };

        await _routeService.UpdateAsync(id, updateDto, ct);
        return Ok(await _routeService.GetAsync(id, ct));
    }

    private string GetLanguage()
    {
        var language = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0]?.Trim() ?? "en";
        if (language.Length > 2) language = language[..2];
        return language;
    }
}

public class RoutingPreviewRequest
{
    public double[][]? Waypoints { get; set; }
    public string? Profile { get; set; }
}

public class RoutingPreviewResult
{
    public double[][] Coordinates { get; set; } = [];
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
}
