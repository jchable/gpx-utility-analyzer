using System.Net;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Processing;

/// <summary>
/// Regression tests for defect A: an activity could be stranded forever.
///
/// Rows in <c>Recovering</c> with an UNEXPIRED lease were skipped at startup and
/// there was no runtime sweeper, so a crash inside the one-minute lease window left
/// the activity untouched until the next restart — and the reanalyze guard returned
/// 202 without acting, removing the manual escape hatch as well.
/// </summary>
[Collection("Integration")]
public class LeaseReclamationTests
{
    /// <summary>A factory whose lease sweeper runs often enough to observe.</summary>
    private static WebApplicationFactory<Program> WithFastSweeper(ApiFactory factory) =>
        factory.WithWebHostBuilder(b => b.UseSetting("Processing:LeaseSweepIntervalSeconds", "1"));

    private static async Task<Activity> ReadAsync(WebApplicationFactory<Program> factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Activities.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    private static async Task<Activity> WaitForTerminalAsync(
        WebApplicationFactory<Program> factory, Guid id, int maxWaitMs = 25_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        Activity activity;
        do
        {
            activity = await ReadAsync(factory, id);
            if (activity.Status is ProcessingStatus.Completed or ProcessingStatus.Failed)
                return activity;
            await Task.Delay(250);
        } while (DateTime.UtcNow < deadline);

        return activity;
    }

    private static Activity Stranded(Guid id, Guid userId, ProcessingStatus status, DateTime? leaseExpiry) => new()
    {
        Id = id,
        UserId = userId,
        Name = "stranded",
        ActivityType = "trail",
        GpxFilePath = "missing.gpx",     // the recovered run must really execute and fail
        Status = status,
        ProcessingLeaseId = Guid.NewGuid(),
        ProcessingLeaseExpiresAt = leaseExpiry,
    };

    private static async Task SeedAsync(WebApplicationFactory<Program> factory, Activity activity)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
    }

    // ── Half 1: expired leases must be reclaimed while the app runs ──────────

    [Fact]
    public async Task ExpiredLease_IsReclaimedWhileTheApplicationIsRunning()
    {
        using var baseFactory = new ApiFactory();
        using var factory = WithFastSweeper(baseFactory);
        var client = factory.CreateClient();   // host start: the startup pass runs on an empty DB
        var auth = await TestHelpers.RegisterAsync(client, $"sweep_{Guid.NewGuid():N}@test.local");

        // A crash inside the lease window: the row is seeded AFTER startup, so only a
        // running sweeper can ever pick it up.
        var id = Guid.NewGuid();
        await SeedAsync(factory, Stranded(
            id, Guid.Parse(auth.User.Id), ProcessingStatus.Recovering,
            DateTime.UtcNow.AddMinutes(-5)));

        var activity = await WaitForTerminalAsync(factory, id);

        Assert.True(activity.Status is ProcessingStatus.Completed or ProcessingStatus.Failed,
            $"activity with an expired lease is still {activity.Status}; nothing reclaimed it");
        Assert.Equal(ProcessingStatus.Failed, activity.Status);
        Assert.False(string.IsNullOrWhiteSpace(activity.ErrorMessage));
    }

    [Fact]
    public async Task LiveLease_IsLeftAloneByTheSweeper()
    {
        using var baseFactory = new ApiFactory();
        using var factory = WithFastSweeper(baseFactory);
        var client = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"live_{Guid.NewGuid():N}@test.local");

        var id = Guid.NewGuid();
        await SeedAsync(factory, Stranded(
            id, Guid.Parse(auth.User.Id), ProcessingStatus.Analyzing,
            DateTime.UtcNow.AddMinutes(30)));   // a worker genuinely holds this

        // Several sweep intervals must pass without the row being touched.
        await Task.Delay(3_000);

        var activity = await ReadAsync(factory, id);
        Assert.Equal(ProcessingStatus.Analyzing, activity.Status);
    }

    // ── Half 2: reanalyze must stay a real escape hatch ──────────────────────

    [Fact]
    public async Task Reanalyze_WhenTheLeaseHasExpired_ActuallyRestartsProcessing()
    {
        using var baseFactory = new ApiFactory();
        // No fast sweeper here: the manual escape hatch has to work on its own.
        var client = baseFactory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"revive_{Guid.NewGuid():N}@test.local");
        var authed = TestHelpers.CreateAuthorizedClient(baseFactory, auth.AccessToken);

        var id = Guid.NewGuid();
        await SeedAsync(baseFactory, Stranded(
            id, Guid.Parse(auth.User.Id), ProcessingStatus.Analyzing,
            DateTime.UtcNow.AddMinutes(-5)));

        var resp = await authed.PostAsync($"/api/activities/{id}/reanalyze", null);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        // 202 must mean something happened: the row has to leave its stuck state.
        var activity = await WaitForTerminalAsync(baseFactory, id);
        Assert.True(activity.Status is ProcessingStatus.Completed or ProcessingStatus.Failed,
            $"reanalyze accepted the request but the activity is still {activity.Status}");
    }

    [Fact]
    public async Task FixAnomalies_WhenTheLeaseHasExpired_ActuallyRestartsProcessing()
    {
        using var baseFactory = new ApiFactory();
        var client = baseFactory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"revivefix_{Guid.NewGuid():N}@test.local");
        var authed = TestHelpers.CreateAuthorizedClient(baseFactory, auth.AccessToken);

        var id = Guid.NewGuid();
        await SeedAsync(baseFactory, Stranded(
            id, Guid.Parse(auth.User.Id), ProcessingStatus.AiProcessing,
            DateTime.UtcNow.AddMinutes(-5)));

        var resp = await authed.PostAsync($"/api/activities/{id}/fix-anomalies", null);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var activity = await WaitForTerminalAsync(baseFactory, id);
        Assert.True(activity.Status is ProcessingStatus.Completed or ProcessingStatus.Failed,
            $"fix-anomalies accepted the request but the activity is still {activity.Status}");
    }

    [Fact]
    public async Task Reanalyze_WhileGenuinelyInFlight_DoesNotStartASecondRun()
    {
        using var baseFactory = new ApiFactory();
        var client = baseFactory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"inflight_{Guid.NewGuid():N}@test.local");
        var authed = TestHelpers.CreateAuthorizedClient(baseFactory, auth.AccessToken);

        var id = Guid.NewGuid();
        var seeded = Stranded(
            id, Guid.Parse(auth.User.Id), ProcessingStatus.Analyzing,
            DateTime.UtcNow.AddMinutes(30));
        await SeedAsync(baseFactory, seeded);

        var resp = await authed.PostAsync($"/api/activities/{id}/reanalyze", null);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        await Task.Delay(1_000);

        // A live lease means a worker owns this run; the request must not steal it.
        var activity = await ReadAsync(baseFactory, id);
        Assert.Equal(ProcessingStatus.Analyzing, activity.Status);
        Assert.Equal(seeded.ProcessingLeaseId, activity.ProcessingLeaseId);
    }
}
