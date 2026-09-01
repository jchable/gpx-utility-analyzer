namespace GpxAnalyzer.Api.Controllers;

using System.Text.Json;
using System.Threading.Channels;
using GpxAnalyzer.Api.BackgroundServices;
using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GpxStorageService _storage;
    private readonly Channel<ProcessingRequest> _processingChannel;
    private readonly ProcessingCancellationRegistry _processingCancellation;
    private readonly GpxAnalysisService _analysisService;
    private readonly ProfileComputationService _profileService;

    public ActivitiesController(
        AppDbContext db,
        GpxStorageService storage,
        Channel<ProcessingRequest> processingChannel,
        ProcessingCancellationRegistry processingCancellation,
        GpxAnalysisService analysisService,
        ProfileComputationService profileService)
    {
        _db = db;
        _storage = storage;
        _processingChannel = processingChannel;
        _processingCancellation = processingCancellation;
        _analysisService = analysisService;
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ActivityListDto>>> GetActivities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null)
    {
        var userId = User.GetUserId();
        var query = _db.Activities.Where(a => a.UserId == userId);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(a => a.ActivityType == type);

        // Materialize first to allow in-memory JSON deserialization for Tags
        var rows = await query
            .OrderByDescending(a => a.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var activities = rows.Select(a => new ActivityListDto
        {
            Id = a.Id,
            Name = a.Name,
            ActivityType = a.ActivityType,
            DetectedSubType = a.DetectedSubType,
            SessionType = a.SessionType,
            Tags = a.Tags != null ? JsonSerializer.Deserialize<string[]>(a.Tags) : null,
            StartTime = a.StartTime,
            DistanceKm = a.DistanceKm,
            ElevationGainM = a.ElevationGainM,
            MovingTimeSeconds = a.MovingTimeSeconds,
            Source = a.Source,
            Status = a.Status.ToString(),
        }).ToList();

        return activities;
    }

    [HttpGet("tags")]
    public async Task<ActionResult<string[]>> GetTags()
    {
        var userId = User.GetUserId();
        var tagsJsonList = await _db.Activities
            .Where(a => a.UserId == userId && a.Tags != null)
            .Select(a => a.Tags!)
            .ToListAsync();

        var allTags = tagsJsonList
            .SelectMany(t => JsonSerializer.Deserialize<string[]>(t) ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToArray();

        return allTags;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityDetailDto>> GetActivity(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        return new ActivityDetailDto
        {
            Id = activity.Id,
            Name = activity.Name,
            ActivityType = activity.ActivityType,
            DetectedSubType = activity.DetectedSubType,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            DistanceKm = activity.DistanceKm,
            ElevationGainM = activity.ElevationGainM,
            ElevationLossM = activity.ElevationLossM,
            MovingTimeSeconds = activity.MovingTimeSeconds,
            Source = activity.Source,
            Status = activity.Status.ToString(),
            ErrorMessage = activity.ErrorMessage,
            Description = activity.Description,
            PerceivedExertion = activity.PerceivedExertion,
            Tags = activity.Tags != null ? JsonSerializer.Deserialize<string[]>(activity.Tags) : null,
            SessionType = activity.SessionType,
            EstimatedCalories = activity.EstimatedCalories,
            CalorieMethod = activity.CalorieMethod,
            Stats = activity.StatsJson is not null ? JsonSerializer.Deserialize<object>(activity.StatsJson) : null,
            AiReport = activity.AiReportJson is not null ? JsonSerializer.Deserialize<object>(activity.AiReportJson) : null,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
        };
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ActivityDetailDto>> Upload(IFormFile file, [FromForm] string? activityType = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "NO_FILE_PROVIDED" });

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { code = "INVALID_FILE_TYPE" });

        using var stream = file.OpenReadStream();
        var relativePath = await _storage.StoreAsync(stream, file.FileName);

        var language = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0]?.Trim() ?? "en";
        if (language.Length > 2) language = language[..2];

        var userId = User.GetUserId();
        var leaseId = Guid.NewGuid();
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Path.GetFileNameWithoutExtension(file.FileName),
            ActivityType = activityType ?? "trail",
            GpxFilePath = relativePath,
            Source = "upload",
            Status = ProcessingStatus.Pending,
            ProcessingLeaseId = leaseId,
            ProcessingLeaseExpiresAt = DateTime.UtcNow.AddMinutes(1),
            Language = language,
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        await _processingChannel.Writer.WriteAsync(new ProcessingRequest(activity.Id, userId, leaseId));

        return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, new ActivityDetailDto
        {
            Id = activity.Id,
            Name = activity.Name,
            Status = activity.Status.ToString(),
            Source = activity.Source,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        // Deleting always succeeds. Signal any in-flight run first so we stop paying
        // for an analysis — and an AI call — whose result has nowhere to go; the
        // worker tolerates the row and the file disappearing underneath it, and
        // storage tolerates a GPX still held open, so we never wait on either (#131).
        _processingCancellation.Cancel(id);

        await _storage.DeleteWithOriginalAsync(activity.GpxFilePath);
        _db.Activities.Remove(activity);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/gpx")]
    public async Task<IActionResult> DownloadGpx(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        if (!await _storage.ExistsAsync(activity.GpxFilePath))
            return NotFound(new { code = "GPX_NOT_FOUND" });

        var stream = await _storage.GetStreamAsync(activity.GpxFilePath);
        return File(stream, "application/gpx+xml", $"{activity.Name}.gpx");
    }

    [HttpGet("{id:guid}/profile")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        if (activity.Status != ProcessingStatus.Completed || activity.ProfileJson is null)
            return NotFound(new { code = "PROFILE_NOT_AVAILABLE" });

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(activity.ProfileJson, "application/json");
    }

    [HttpGet("{id:guid}/track")]
    public async Task<IActionResult> GetTrack(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        if (activity.Status != ProcessingStatus.Completed || activity.TrackGeoJson is null)
            return NotFound(new { code = "TRACK_NOT_AVAILABLE" });

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(activity.TrackGeoJson, "application/json");
    }

    [HttpGet("{id:guid}/splits")]
    public async Task<IActionResult> GetSplits(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        if (activity.Status != ProcessingStatus.Completed || activity.SplitsJson is null)
            return NotFound(new { code = "SPLITS_NOT_AVAILABLE" });

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(activity.SplitsJson, "application/json");
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateActivity(Guid id, [FromBody] UpdateActivityDto dto)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        if (!string.IsNullOrEmpty(dto.ActivityType))
            activity.ActivityType = dto.ActivityType;
        if (!string.IsNullOrEmpty(dto.Name))
            activity.Name = dto.Name;
        if (dto.Description is not null)
            activity.Description = string.IsNullOrEmpty(dto.Description) ? null : dto.Description;
        if (dto.PerceivedExertion.HasValue)
            activity.PerceivedExertion = dto.PerceivedExertion.Value is >= 1 and <= 10
                ? dto.PerceivedExertion.Value
                : null;
        if (dto.Tags is not null)
            activity.Tags = dto.Tags.Length > 0 ? JsonSerializer.Serialize(dto.Tags) : null;
        if (dto.SessionType is not null)
            activity.SessionType = string.IsNullOrEmpty(dto.SessionType) ? null : dto.SessionType;

        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { activity.Id, activity.ActivityType, activity.Name });
    }

    [HttpPost("{id:guid}/reanalyze")]
    public async Task<IActionResult> Reanalyze(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        var language = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0]?.Trim() ?? "en";
        if (language.Length > 2) language = language[..2];

        if (IsOwnedByALiveWorker(activity))
            return Accepted();

        var leaseId = Guid.NewGuid();
        activity.Status = ProcessingStatus.Pending;
        activity.ProcessingLeaseId = leaseId;
        activity.ProcessingLeaseExpiresAt = DateTime.UtcNow.AddMinutes(1);
        activity.ErrorMessage = null;
        activity.AiReportJson = null;
        activity.ProfileJson = null;
        activity.TrackGeoJson = null;
        activity.SplitsJson = null;
        activity.Language = language;
        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _processingChannel.Writer.WriteAsync(new ProcessingRequest(id, User.GetUserId(), leaseId));

        return Accepted();
    }

    /// <summary>
    /// Whether a worker genuinely owns this activity right now: a non-terminal
    /// status AND a lease that has not run out.
    ///
    /// The status alone is not enough. A crash leaves the row non-terminal with a
    /// lease nobody holds, and answering 202 to that without acting is how an
    /// activity ends up unrevivable — the user's only manual escape hatch silently
    /// does nothing. Once the lease has expired the row is fair game to restart.
    /// </summary>
    private static bool IsOwnedByALiveWorker(Activity activity) =>
        activity.Status is ProcessingStatus.Pending or ProcessingStatus.Recovering
            or ProcessingStatus.Analyzing or ProcessingStatus.AiProcessing
        && activity.ProcessingLeaseExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Re-triggers full processing with anomaly correction enabled.
    /// Sets FixAnomaliesOnNextRun = true so the pipeline runs with --fix-anomalies.
    /// </summary>
    [HttpPost("{id:guid}/fix-anomalies")]
    public async Task<IActionResult> FixAnomalies(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null || activity.UserId != User.GetUserId()) return NotFound();

        var language = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0]?.Trim() ?? "en";
        if (language.Length > 2) language = language[..2];

        if (IsOwnedByALiveWorker(activity))
            return Accepted();

        var leaseId = Guid.NewGuid();
        activity.FixAnomaliesOnNextRun = true;
        activity.Status = ProcessingStatus.Pending;
        activity.ProcessingLeaseId = leaseId;
        activity.ProcessingLeaseExpiresAt = DateTime.UtcNow.AddMinutes(1);
        activity.ErrorMessage = null;
        activity.AiReportJson = null;
        activity.ProfileJson = null;
        activity.TrackGeoJson = null;
        activity.SplitsJson = null;
        activity.Language = language;
        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _processingChannel.Writer.WriteAsync(new ProcessingRequest(id, User.GetUserId(), leaseId));

        return Accepted();
    }
}
