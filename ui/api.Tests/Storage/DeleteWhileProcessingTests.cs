using System.Net;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Storage;

/// <summary>
/// Regression tests for #131 — DELETE /api/activities/{id} returned 500 when the
/// background worker still held the GPX file open:
/// LocalStorageService.DeleteAsync → File.Delete → IOException escaping through
/// ActivitiesController.Delete.
///
/// Defined behaviour: a delete always succeeds. Any in-flight processing is
/// cancelled, a GPX file that cannot be removed right now is left behind rather
/// than failing the request, and the worker tolerates its row or its file
/// disappearing underneath it.
/// </summary>
[Collection("Integration")]
public class DeleteWhileProcessingTests
{
    private static string StoredGpxPath(ApiFactory factory, string relativePath)
    {
        using var scope = factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        return Path.Combine(config["Storage:GpxDirectory"]!, relativePath);
    }

    private static async Task<Activity> GetActivityAsync(ApiFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Activities.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    // ── The measured failure: File.Delete on a file another handle holds ──────

    [Fact]
    public async Task Delete_WhileTheGpxFileIsHeldOpen_StillSucceeds()
    {
        await using var factory = new ApiFactory();
        var anon = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, $"locked_{Guid.NewGuid():N}@test.local");
        var client = TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);

        var id = await TestHelpers.UploadTestGpxAsync(client);
        Assert.Equal("Completed", await TestHelpers.WaitForProcessingAsync(client, id));

        var activity = await GetActivityAsync(factory, Guid.Parse(id));
        var gpxPath = StoredGpxPath(factory, activity.GpxFilePath);
        Assert.True(File.Exists(gpxPath), $"expected a stored GPX at {gpxPath}");

        // Exactly what the worker does mid-run: hold a read handle that does NOT
        // share delete. On Windows this makes File.Delete throw IOException.
        using (var held = new FileStream(gpxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var resp = await client.DeleteAsync($"/api/activities/{id}");

            Assert.False((int)resp.StatusCode >= 500,
                $"delete returned {(int)resp.StatusCode} while the GPX was held open");
            Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        }

        // The activity itself is gone regardless of what happened to the file.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/activities/{id}")).StatusCode);
    }

    // ── Deliberately concurrent: delete racing the worker ────────────────────

    [Fact]
    public async Task Delete_ConcurrentWithProcessing_NeverReturnsAServerError()
    {
        await using var factory = new ApiFactory();
        var anon = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, $"race_{Guid.NewGuid():N}@test.local");
        var client = TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);

        // Three rounds of eight, deleted the instant the upload returns — the
        // worker is guaranteed to be mid-pipeline on several of them. Deliberate,
        // rather than hoping incidental suite load produces the race.
        for (var round = 0; round < 3; round++)
        {
            var ids = new List<string>();
            for (var i = 0; i < 8; i++)
                ids.Add(await TestHelpers.UploadTestGpxAsync(client));

            var responses = await Task.WhenAll(
                ids.Select(id => client.DeleteAsync($"/api/activities/{id}")));

            foreach (var (resp, id) in responses.Zip(ids))
                Assert.True(resp.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound,
                    $"round {round}: delete of {id} returned {(int)resp.StatusCode} {resp.StatusCode}");
        }

        // Every row really is gone, and the worker survived to keep serving.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Activities.AsNoTracking().ToListAsync());
    }

    // ── The worker must tolerate its row vanishing mid-run ───────────────────

    [Fact]
    public async Task Processing_WhenTheRowIsDeletedMidRun_DoesNotThrow()
    {
        await using var factory = new ApiFactory();
        var anon = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, $"vanish_{Guid.NewGuid():N}@test.local");

        var activityId = Guid.NewGuid();
        var userId = Guid.Parse(auth.User.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed through this scope's own DbContext so the entity stays tracked:
        // FindAsync then hands it back without a round-trip, which is exactly the
        // state the worker is in when a concurrent DELETE removes the row.
        db.Activities.Add(new Activity
        {
            Id = activityId,
            UserId = userId,
            Name = "deleted mid-run",
            ActivityType = "trail",
            GpxFilePath = "missing.gpx",
            Status = ProcessingStatus.Pending,
        });
        await db.SaveChangesAsync();

        // A concurrent request deletes the row.
        using (var other = factory.Services.CreateScope())
        {
            var otherDb = other.ServiceProvider.GetRequiredService<AppDbContext>();
            await otherDb.Activities.Where(a => a.Id == activityId).ExecuteDeleteAsync();
        }

        var service = scope.ServiceProvider.GetRequiredService<ActivityProcessingService>();

        // Must not surface DbUpdateConcurrencyException: the row it is writing to
        // no longer exists, which is a normal outcome, not a worker crash.
        await service.ProcessActivityAsync(activityId, userId);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await verifyDb.Activities.AnyAsync(a => a.Id == activityId));
    }
}
