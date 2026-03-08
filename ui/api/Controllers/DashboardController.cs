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
        // Use SQL-level aggregation instead of materializing all activities in memory.
        // Pattern: nullable Select + SumAsync to handle empty sets in SQLite.
        var completed = _db.Activities.Where(a => a.Status == ProcessingStatus.Completed);

        var totalActivities = await completed.CountAsync();
        var totalDistanceKm = await completed.Select(a => (double?)a.DistanceKm).SumAsync() ?? 0;
        var totalElevationGainM = await completed.Select(a => (double?)a.ElevationGainM).SumAsync() ?? 0;
        var totalMovingTimeSeconds = await completed.Select(a => (double?)a.MovingTimeSeconds).SumAsync() ?? 0;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var thisMonth = completed.Where(a => a.StartTime >= monthStart);

        var activitiesThisMonth = await thisMonth.CountAsync();
        var distanceThisMonthKm = await thisMonth.Select(a => (double?)a.DistanceKm).SumAsync() ?? 0;
        var elevationGainThisMonthM = await thisMonth.Select(a => (double?)a.ElevationGainM).SumAsync() ?? 0;
        var movingTimeThisMonthSeconds = await thisMonth.Select(a => (double?)a.MovingTimeSeconds).SumAsync() ?? 0;

        // Activity type breakdown: materialize only the type column
        var typeBreakdown = await completed
            .GroupBy(a => a.ActivityType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        // Project only the columns needed for the list — avoids loading
        // ProfileJson, TrackGeoJson, StatsJson, AiReportJson, SplitsJson blobs.
        var recentActivities = await completed
            .OrderByDescending(a => a.StartTime)
            .Take(10)
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

        return new DashboardSummaryDto
        {
            TotalActivities = totalActivities,
            TotalDistanceKm = totalDistanceKm,
            TotalElevationGainM = totalElevationGainM,
            TotalMovingTimeSeconds = totalMovingTimeSeconds,
            ActivitiesThisMonth = activitiesThisMonth,
            DistanceThisMonthKm = distanceThisMonthKm,
            ElevationGainThisMonthM = elevationGainThisMonthM,
            MovingTimeThisMonthSeconds = movingTimeThisMonthSeconds,
            RecentActivities = recentActivities,
            ActivityTypeBreakdown = typeBreakdown.ToDictionary(g => g.Type, g => g.Count),
        };
    }
}
