namespace GpxAnalyzer.Api.Controllers;

using System.Text.Json;
using System.Threading.Channels;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GpxStorageService _storage;
    private readonly Channel<Guid> _processingChannel;

    public ActivitiesController(AppDbContext db, GpxStorageService storage, Channel<Guid> processingChannel)
    {
        _db = db;
        _storage = storage;
        _processingChannel = processingChannel;
    }

    [HttpGet]
    public async Task<ActionResult<List<ActivityListDto>>> GetActivities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null)
    {
        var query = _db.Activities.AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(a => a.ActivityType == type);

        var activities = await query
            .OrderByDescending(a => a.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityListDto
            {
                Id = a.Id,
                Name = a.Name,
                ActivityType = a.ActivityType,
                StartTime = a.StartTime,
                DistanceKm = a.DistanceKm,
                ElevationGainM = a.ElevationGainM,
                MovingTimeSeconds = a.MovingTimeSeconds,
                Source = a.Source,
                Status = a.Status.ToString(),
            })
            .ToListAsync();

        return activities;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityDetailDto>> GetActivity(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null) return NotFound();

        return new ActivityDetailDto
        {
            Id = activity.Id,
            Name = activity.Name,
            ActivityType = activity.ActivityType,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            DistanceKm = activity.DistanceKm,
            ElevationGainM = activity.ElevationGainM,
            ElevationLossM = activity.ElevationLossM,
            MovingTimeSeconds = activity.MovingTimeSeconds,
            Source = activity.Source,
            Status = activity.Status.ToString(),
            ErrorMessage = activity.ErrorMessage,
            Stats = activity.StatsJson is not null ? JsonSerializer.Deserialize<object>(activity.StatsJson) : null,
            AiReport = activity.AiReportJson is not null ? JsonSerializer.Deserialize<object>(activity.AiReportJson) : null,
            CreatedAt = activity.CreatedAt,
        };
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ActivityDetailDto>> Upload(IFormFile file, [FromForm] string? activityType = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .gpx files are supported.");

        using var stream = file.OpenReadStream();
        var relativePath = await _storage.StoreAsync(stream, file.FileName);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(file.FileName),
            ActivityType = activityType ?? "trail",
            GpxFilePath = relativePath,
            Source = "upload",
            Status = ProcessingStatus.Pending,
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        await _processingChannel.Writer.WriteAsync(activity.Id);

        return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, new ActivityDetailDto
        {
            Id = activity.Id,
            Name = activity.Name,
            Status = activity.Status.ToString(),
            Source = activity.Source,
            CreatedAt = activity.CreatedAt,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null) return NotFound();

        _storage.DeleteWithOriginal(activity.GpxFilePath);
        _db.Activities.Remove(activity);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/gpx")]
    public async Task<IActionResult> DownloadGpx(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null) return NotFound();

        var fullPath = _storage.GetFullPath(activity.GpxFilePath);
        if (!System.IO.File.Exists(fullPath)) return NotFound("GPX file not found.");

        return PhysicalFile(Path.GetFullPath(fullPath), "application/gpx+xml", $"{activity.Name}.gpx");
    }

    [HttpPost("{id:guid}/reanalyze")]
    public async Task<IActionResult> Reanalyze(Guid id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity is null) return NotFound();

        activity.Status = ProcessingStatus.Pending;
        activity.ErrorMessage = null;
        activity.AiReportJson = null;
        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _processingChannel.Writer.WriteAsync(id);

        return Accepted();
    }
}
