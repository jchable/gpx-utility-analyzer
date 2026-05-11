namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/race-plans")]
public class RacePlansController : ControllerBase
{
    private readonly RacePlanService _service;

    public RacePlansController(RacePlanService service)
    {
        _service = service;
    }

    // ─────────────────────────────────────────────
    // List / Get
    // ─────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<RacePlanListDto>>> GetPlans(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var plans = await _service.ListAsync(userId, page, pageSize, type, status, ct);
        return plans;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RacePlanDetailDto>> GetPlan(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var plan = await _service.GetAsync(userId, id, ct);
        if (plan is null) return NotFound();
        return plan;
    }

    // Vue partagée crew (pas d'authentification requise)
    [AllowAnonymous]
    [HttpGet("share/{token}")]
    public async Task<ActionResult<RacePlanSharedDto>> GetShared(string token, CancellationToken ct = default)
    {
        var plan = await _service.GetSharedAsync(token, ct);
        if (plan is null) return NotFound();
        return plan;
    }

    // ─────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────

    [HttpPost("from-route/{routeId:guid}")]
    public async Task<ActionResult<RacePlanDetailDto>> CreateFromRoute(
        Guid routeId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var language = GetLanguage();
        try
        {
            var plan = await _service.CreateFromRouteAsync(userId, routeId, language, ct);
            var detail = await _service.GetAsync(userId, plan.Id, ct);
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, detail);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult<RacePlanDetailDto>> ImportGpx(
        IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "NO_FILE_PROVIDED" });

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { code = "INVALID_FILE_TYPE" });

        var userId = User.GetUserId();
        var language = GetLanguage();
        using var stream = file.OpenReadStream();
        var plan = await _service.CreateFromGpxAsync(userId, stream, file.FileName, language, ct);
        if (plan is null) return BadRequest(new { code = "EMPTY_GPX_FILE" });

        var detail = await _service.GetAsync(userId, plan.Id, ct);
        return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, detail);
    }

    // ─────────────────────────────────────────────
    // Update / Delete
    // ─────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RacePlanDetailDto>> UpdatePlan(
        Guid id, [FromBody] RacePlanUpdateDto dto, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var plan = await _service.UpdateAsync(userId, id, dto, ct);
        if (plan is null) return NotFound();
        var detail = await _service.GetAsync(userId, id, ct);
        return Ok(detail);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.DeleteAsync(userId, id, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─────────────────────────────────────────────
    // Checkpoints
    // ─────────────────────────────────────────────

    [HttpPost("{id:guid}/checkpoints")]
    public async Task<ActionResult<RacePlanCheckpointDto>> AddCheckpoint(
        Guid id, [FromBody] RacePlanCheckpointCreateDto dto, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var checkpoint = await _service.AddCheckpointAsync(userId, id, dto, ct);
        if (checkpoint is null) return NotFound();

        // Retourner le plan mis à jour
        var plan = await _service.GetAsync(userId, id, ct);
        return Ok(plan);
    }

    [HttpPut("{id:guid}/checkpoints/{checkpointId:guid}")]
    public async Task<ActionResult<RacePlanDetailDto>> UpdateCheckpoint(
        Guid id, Guid checkpointId, [FromBody] RacePlanCheckpointUpdateDto dto,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var checkpoint = await _service.UpdateCheckpointAsync(userId, id, checkpointId, dto, ct);
        if (checkpoint is null) return NotFound();

        var plan = await _service.GetAsync(userId, id, ct);
        return Ok(plan);
    }

    [HttpDelete("{id:guid}/checkpoints/{checkpointId:guid}")]
    public async Task<IActionResult> DeleteCheckpoint(
        Guid id, Guid checkpointId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.DeleteCheckpointAsync(userId, id, checkpointId, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─────────────────────────────────────────────
    // Recalcul des temps
    // ─────────────────────────────────────────────

    [HttpPost("{id:guid}/compute-times")]
    public async Task<ActionResult<RacePlanDetailDto>> ComputeTimes(
        Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.ComputeTimesAsync(userId, id, ct);
        if (!success) return NotFound();

        var plan = await _service.GetAsync(userId, id, ct);
        return Ok(plan);
    }

    // ─────────────────────────────────────────────
    // Nutrition
    // ─────────────────────────────────────────────

    [HttpPost("{id:guid}/nutrition")]
    public async Task<ActionResult<RacePlanDetailDto>> AddNutritionItem(
        Guid id, [FromBody] RacePlanNutritionItemCreateDto dto, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var item = await _service.AddNutritionItemAsync(userId, id, dto, ct);
        if (item is null) return NotFound();

        var plan = await _service.GetAsync(userId, id, ct);
        return Ok(plan);
    }

    [HttpDelete("{id:guid}/nutrition/{itemId:guid}")]
    public async Task<IActionResult> DeleteNutritionItem(
        Guid id, Guid itemId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.DeleteNutritionItemAsync(userId, id, itemId, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─────────────────────────────────────────────
    // Partage crew
    // ─────────────────────────────────────────────

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<object>> EnableShare(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var token = await _service.EnableShareAsync(userId, id, ct);
        if (token is null) return NotFound();
        return Ok(new { token, shareUrl = $"/share/race-plan/{token}" });
    }

    [HttpDelete("{id:guid}/share")]
    public async Task<IActionResult> DisableShare(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.DisableShareAsync(userId, id, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─────────────────────────────────────────────
    // Post-course
    // ─────────────────────────────────────────────

    [HttpPost("{id:guid}/link-activity/{activityId:guid}")]
    public async Task<IActionResult> LinkActivity(
        Guid id, Guid activityId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.LinkActivityAsync(userId, id, activityId, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:guid}/comparison")]
    public async Task<ActionResult<RacePlanComparisonDto>> GetComparison(
        Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var comparison = await _service.GetComparisonAsync(userId, id, ct);
        if (comparison is null) return NotFound();
        return comparison;
    }

    // ─────────────────────────────────────────────

    private string GetLanguage()
    {
        var language = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0]?.Trim() ?? "en";
        if (language.Length > 2) language = language[..2];
        return language;
    }
}
