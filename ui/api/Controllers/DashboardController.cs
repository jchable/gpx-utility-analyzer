namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        // Materialize in memory to avoid EF Core SQLite translation issues
        // with enum string conversions + DateTimeOffset comparisons
        var completedActivities = await _db.Activities
            .Where(a => a.Status == ProcessingStatus.Completed)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var thisMonth = completedActivities.Where(a => a.StartTime >= monthStart).ToList();

        var recentActivities = await _db.Activities
            .OrderByDescending(a => a.StartTime)
            .Take(10)
            .ToListAsync();

        return new DashboardSummaryDto
        {
            TotalActivities = completedActivities.Count,
            TotalDistanceKm = completedActivities.Sum(a => a.DistanceKm),
            TotalElevationGainM = completedActivities.Sum(a => a.ElevationGainM),
            TotalMovingTimeSeconds = completedActivities.Sum(a => a.MovingTimeSeconds),
            ActivitiesThisMonth = thisMonth.Count,
            DistanceThisMonthKm = thisMonth.Sum(a => a.DistanceKm),
            RecentActivities = recentActivities.Select(a => new ActivityListDto
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
            }).ToList(),
            ActivityTypeBreakdown = completedActivities
                .GroupBy(a => a.ActivityType)
                .ToDictionary(g => g.Key, g => g.Count()),
        };
    }
}
