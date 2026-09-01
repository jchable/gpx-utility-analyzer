using System.Globalization;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Processing;

/// <summary>
/// Regression tests for #113 (activity timestamps stored in the host's local time
/// instead of UTC) and #114 (activities stranded in a non-terminal status by a
/// restart, with nothing to move them on).
/// </summary>
[Collection("Integration")]
public class ProcessingRecoveryTests
{
    // The first and last <time> in ui/api.Tests/Fixtures/test.gpx.
    private static readonly DateTime FixtureStartUtc = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixtureEndUtc = new(2024, 1, 1, 10, 20, 0, DateTimeKind.Utc);

    // ── #113 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Characterises the expression the service used to use. This assertion holds on
    /// EVERY host, including one whose local time is UTC: with DateTimeStyles.None a
    /// trailing Z means "convert to local time", and the result is stamped Local
    /// unconditionally — the offset being zero does not turn it back into Utc. That is
    /// what makes the Kind assertion below a timezone-robust regression guard rather
    /// than something that only trips on a machine with a non-zero UTC offset.
    /// </summary>
    [Fact]
    public void DefaultTryParse_StampsLocalKind_EvenWhenTheOffsetIsZero()
    {
        Assert.True(DateTime.TryParse("2024-01-01T10:00:00Z", out var withZ));
        Assert.Equal(DateTimeKind.Local, withZ.Kind);

        // Same shape as a UTC host parsing a Z string: the offset equals the host's
        // own, so the value is untouched — and the Kind is still Local, not Utc.
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2024, 1, 1, 10, 0, 0));
        var matchingOffset = $"2024-01-01T10:00:00{localOffset.Hours:+00;-00}:{Math.Abs(localOffset.Minutes):00}";
        Assert.True(DateTime.TryParse(matchingOffset, out var withLocalOffset));
        Assert.Equal(new DateTime(2024, 1, 1, 10, 0, 0), withLocalOffset);
        Assert.Equal(DateTimeKind.Local, withLocalOffset.Kind);
    }

    [Theory]
    [InlineData("2024-01-01T10:00:00Z")]   // winter — Europe/Paris is UTC+1
    [InlineData("2024-06-30T23:30:00Z")]   // summer — UTC+2, i.e. a different scale
    public void CliTimestamp_IsParsedAsAUtcInstant(string emitted)
    {
        // The instant the string denotes, independent of the host's timezone.
        var expected = DateTimeOffset.Parse(emitted, CultureInfo.InvariantCulture).UtcDateTime;

        Assert.True(ActivityProcessingService.TryParseUtc(emitted, out var parsed));

        Assert.Equal(DateTimeKind.Utc, parsed.Kind);   // fails on a UTC host too
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public async Task UploadedActivity_StoresStartAndEndTimeInUtc()
    {
        await using var factory = new ApiFactory();
        var anon = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, $"utc_{Guid.NewGuid():N}@test.local");
        var authed = TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);

        var id = await TestHelpers.UploadTestGpxAsync(authed);
        var status = await TestHelpers.WaitForProcessingAsync(authed, id);
        Assert.Equal("Completed", status);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = await db.Activities.SingleAsync(a => a.Id == Guid.Parse(id));

        // Everything else in the schema is UTC (CreatedAt/UpdatedAt are UtcNow, and
        // DashboardController builds its month boundary from UtcNow), so these must be
        // the fixture's own UTC wall-clock values, not the host's local rendering.
        Assert.Equal(FixtureStartUtc, activity.StartTime);
        Assert.Equal(FixtureEndUtc, activity.EndTime);
    }

    // ── #114 ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Startup_RequeuesActivitiesLeftInANonTerminalState()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"recovery_{Guid.NewGuid():N}.db");
        var strandedId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var untouchedId = Guid.NewGuid();
        var untouchedUpdatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // First "process": a user plus activities the host was killed in the middle of.
        await using (var factory = new ApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            var auth = await TestHelpers.RegisterAsync(client, $"stuck_{Guid.NewGuid():N}@test.local");
            var userId = Guid.Parse(auth.User.Id);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Activities.AddRange(
                new Activity
                {
                    Id = strandedId,
                    UserId = userId,
                    Name = "interrupted",
                    ActivityType = "trail",
                    GpxFilePath = "missing.gpx",
                    Status = ProcessingStatus.Analyzing,   // killed mid-DEM-download
                },
                new Activity
                {
                    Id = pendingId,
                    UserId = userId,
                    Name = "queued-then-lost",
                    ActivityType = "trail",
                    GpxFilePath = "missing.gpx",
                    Status = ProcessingStatus.Pending,     // id died with the Channel
                },
                new Activity
                {
                    Id = untouchedId,
                    UserId = userId,
                    Name = "already-done",
                    ActivityType = "trail",
                    GpxFilePath = "missing.gpx",
                    Status = ProcessingStatus.Completed,   // terminal: must be left alone
                    UpdatedAt = untouchedUpdatedAt,
                });
            await db.SaveChangesAsync();
        }

        // The rows really are stranded once that host is gone.
        await using (var factory = new ApiFactory(dbPath))
        {
            _ = factory.CreateClient();   // forces host start, which runs IHostedServices

            var deadline = DateTime.UtcNow.AddSeconds(20);
            Activity? stranded = null;
            Activity? pending = null;
            while (DateTime.UtcNow < deadline)
            {
                using (var scope = factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    stranded = await db.Activities.AsNoTracking().SingleAsync(a => a.Id == strandedId);
                    pending = await db.Activities.AsNoTracking().SingleAsync(a => a.Id == pendingId);
                }
                if (IsTerminal(stranded.Status) && IsTerminal(pending.Status)) break;
                await Task.Delay(300);
            }

            // Both must reach a TERMINAL state instead of sitting there forever.
            Assert.True(IsTerminal(stranded!.Status),
                $"stranded activity is still {stranded.Status} after restart");
            Assert.True(IsTerminal(pending!.Status),
                $"queued activity is still {pending.Status} after restart");

            // The GPX is missing, so the recovered run must genuinely have executed the
            // pipeline and failed on it — not merely had a status column rewritten.
            Assert.Equal(ProcessingStatus.Failed, stranded.Status);
            Assert.False(string.IsNullOrWhiteSpace(stranded.ErrorMessage));
            Assert.True(stranded.UpdatedAt > DateTime.UtcNow.AddMinutes(-5),
                "the recovered activity was never re-processed (UpdatedAt is stale)");

            // A terminal row is not touched by recovery.
            using var verify = factory.Services.CreateScope();
            var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var untouched = await verifyDb.Activities.AsNoTracking().SingleAsync(a => a.Id == untouchedId);
            Assert.Equal(ProcessingStatus.Completed, untouched.Status);
            Assert.Equal(untouchedUpdatedAt, untouched.UpdatedAt);
        }

        try { File.Delete(dbPath); } catch { /* best-effort */ }
    }

    private static bool IsTerminal(ProcessingStatus status) =>
        status is ProcessingStatus.Completed or ProcessingStatus.Failed;

    /// <summary>
    /// The failure write must not use the token that caused the failure: on host
    /// shutdown the pipeline throws OperationCanceledException from `ct` and saving
    /// with that same token throws again, leaving the row on its last committed value.
    /// </summary>
    [Fact]
    public async Task ProcessingCancelled_StillCommitsTheFailedStatus()
    {
        await using var factory = new ApiFactory();
        var anon = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, $"cancel_{Guid.NewGuid():N}@test.local");

        var activityId = Guid.NewGuid();
        var userId = Guid.Parse(auth.User.Id);

        using (var scope = factory.Services.CreateScope())
        {
            // Seed through the scope's own DbContext so the entity is already tracked:
            // FindAsync then returns it without touching the token, which puts the
            // cancellation exactly where a host shutdown puts it — inside the try.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Activities.Add(new Activity
            {
                Id = activityId,
                UserId = userId,
                Name = "cancelled",
                ActivityType = "trail",
                GpxFilePath = "missing.gpx",
                Status = ProcessingStatus.Pending,
            });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<ActivityProcessingService>();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            // Must not rethrow: the whole point is that the catch block writes Failed.
            await service.ProcessActivityAsync(activityId, userId, cts.Token);
        }

        using (var verifyScope = factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activity = await db.Activities.AsNoTracking().SingleAsync(a => a.Id == activityId);
            Assert.Equal(ProcessingStatus.Failed, activity.Status);
        }
    }

    [Fact]
    public async Task ProcessingClaim_WithWrongUserOrLease_LeavesActivityPending()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"lease_{Guid.NewGuid():N}@test.local");
        var activityId = Guid.NewGuid();
        var userId = Guid.Parse(auth.User.Id);
        var leaseId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Activities.Add(new Activity
            {
                Id = activityId,
                UserId = userId,
                Name = "leased",
                ActivityType = "trail",
                GpxFilePath = "missing.gpx",
                Status = ProcessingStatus.Pending,
                ProcessingLeaseId = leaseId,
                ProcessingLeaseExpiresAt = DateTime.UtcNow.AddMinutes(1),
            });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<ActivityProcessingService>();
            await service.ProcessActivityAsync(activityId, Guid.NewGuid(), leaseId);
            await service.ProcessActivityAsync(activityId, userId, Guid.NewGuid());
        }

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = await verifyDb.Activities.AsNoTracking().SingleAsync(a => a.Id == activityId);
        Assert.Equal(ProcessingStatus.Pending, activity.Status);
        Assert.Equal(leaseId, activity.ProcessingLeaseId);
    }
}
