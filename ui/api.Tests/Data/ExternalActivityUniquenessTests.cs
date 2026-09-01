using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Data;

/// <summary>
/// Regression tests for the unique index on imported external activities.
///
/// Two defects in the shipped index:
///   1. It omitted <c>UserId</c>, so two users could not both hold the same
///      provider activity — two runners from the same club sharing a workout.
///   2. There was no de-duplication step, so the migration threw
///      "UNIQUE constraint failed" on any database that already held the very
///      duplicates the index was introduced to prevent.
/// </summary>
[Collection("Integration")]
public class ExternalActivityUniquenessTests
{
    /// <summary>The migration immediately before the uniqueness migration.</summary>
    private const string PreviousMigration = "20260330200131_AddSweatRate";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext OpenContext(string dbPath) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options);

    /// <summary>
    /// The exact TEXT SQLite holds for a user's primary key. Activities.UserId is a
    /// TEXT foreign key, and SQLite compares TEXT byte-for-byte — so a hand-built
    /// INSERT has to reuse the stored spelling rather than re-render the Guid.
    /// </summary>
    private static async Task<string> StoredUserKeyAsync(AppDbContext db, Guid userId)
    {
        var keys = await db.Database
            .SqlQueryRaw<string>("""SELECT CAST("Id" AS TEXT) AS "Value" FROM "AspNetUsers" """)
            .ToListAsync();
        return keys.Single(k => Guid.Parse(k) == userId);
    }

    private static async Task InsertActivityAsync(
        AppDbContext db, Guid id, string userId, string name,
        string source, string? externalId, DateTime createdAt)
    {
        const string columns =
            "INSERT INTO \"Activities\" " +
            "(\"Id\",\"UserId\",\"Name\",\"ActivityType\",\"StartTime\",\"EndTime\"," +
            " \"DistanceKm\",\"ElevationGainM\",\"ElevationLossM\",\"MovingTimeSeconds\"," +
            " \"GpxFilePath\",\"Source\",\"ExternalId\",\"Status\",\"Language\"," +
            " \"CreatedAt\",\"UpdatedAt\",\"FixAnomaliesOnNextRun\") VALUES ";

        // The ExternalId placeholder has to be a literal NULL rather than a parameter:
        // EF's raw-SQL builder needs a store type for every parameter and has none
        // for DBNull.
        var values =
            "({0},{1},{2},'trail',{4},{4},0,0,0,0,'x.gpx',{3}," +
            (externalId is null ? "NULL" : "{5}") +
            ",'Completed','en',{4},{4},0)";

        object[] parameters = externalId is null
            ? [id.ToString(), userId, name, source, createdAt.ToString("yyyy-MM-dd HH:mm:ss")]
            : [id.ToString(), userId, name, source, createdAt.ToString("yyyy-MM-dd HH:mm:ss"), externalId];

        await db.Database.ExecuteSqlRawAsync(columns + values, parameters);
    }

    /// <summary>
    /// Builds a database at the PREVIOUS migration holding two real users, then
    /// hands it back so the caller can seed pre-existing rows before migrating up.
    /// </summary>
    private static async Task<(string DbPath, Guid Alice, Guid Bob)> SeedLegacyDatabaseAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"uniq_{Guid.NewGuid():N}.db");

        Guid alice, bob;
        await using (var factory = new ApiFactory(dbPath))
        {
            var client = factory.CreateClient();
            alice = Guid.Parse((await TestHelpers.RegisterAsync(client, $"alice_{Guid.NewGuid():N}@test.local")).User.Id);
            bob = Guid.Parse((await TestHelpers.RegisterAsync(client, $"bob_{Guid.NewGuid():N}@test.local")).User.Id);
        }

        // Roll the schema back to the state a deployed instance would be in.
        await using (var db = OpenContext(dbPath))
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        return (dbPath, alice, bob);
    }

    // ── Defect C.2: the migration must survive pre-existing duplicates ───────

    [Fact]
    public async Task Migration_OnADatabaseHoldingDuplicates_KeepsTheOldestRowPerUser()
    {
        var (dbPath, alice, bob) = await SeedLegacyDatabaseAsync();
        try
        {
            var oldest = Guid.NewGuid();
            var newer = Guid.NewGuid();
            var newest = Guid.NewGuid();
            var bobsCopy = Guid.NewGuid();

            await using (var db = OpenContext(dbPath))
            {
                var aliceKey = await StoredUserKeyAsync(db, alice);
                var bobKey = await StoredUserKeyAsync(db, bob);

                // Alice imported the same Strava activity three times.
                await InsertActivityAsync(db, oldest, aliceKey, "first import", "strava", "555", new DateTime(2026, 1, 1));
                await InsertActivityAsync(db, newer, aliceKey, "second import", "strava", "555", new DateTime(2026, 2, 1));
                await InsertActivityAsync(db, newest, aliceKey, "third import", "strava", "555", new DateTime(2026, 3, 1));

                // Bob shared the same workout — a legitimate row that must survive.
                await InsertActivityAsync(db, bobsCopy, bobKey, "bob's copy", "strava", "555", new DateTime(2026, 2, 15));

                // Uploads carry a NULL ExternalId and must never be touched.
                await InsertActivityAsync(db, Guid.NewGuid(), aliceKey, "upload a", "upload", null, new DateTime(2026, 1, 5));
                await InsertActivityAsync(db, Guid.NewGuid(), aliceKey, "upload b", "upload", null, new DateTime(2026, 1, 6));
            }

            // Applying the migration must NOT throw on the duplicates.
            await using (var db = OpenContext(dbPath))
                await db.Database.MigrateAsync();

            await using (var verify = OpenContext(dbPath))
            {
                var stravaRows = await verify.Activities
                    .Where(a => a.Source == "strava")
                    .AsNoTracking()
                    .ToListAsync();

                // Only the OLDEST of Alice's three, plus Bob's independent copy.
                Assert.Equal(2, stravaRows.Count);
                Assert.Contains(stravaRows, a => a.Id == oldest);
                Assert.Contains(stravaRows, a => a.Id == bobsCopy);
                Assert.DoesNotContain(stravaRows, a => a.Id == newer);
                Assert.DoesNotContain(stravaRows, a => a.Id == newest);

                // The NULL-ExternalId uploads are untouched.
                Assert.Equal(2, await verify.Activities.CountAsync(a => a.Source == "upload"));
            }
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best-effort */ }
        }
    }

    // ── Defect C.1: the index must be scoped per user ────────────────────────

    [Fact]
    public async Task TwoUsers_CanEachHoldTheSameProviderActivity()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var alice = Guid.Parse((await TestHelpers.RegisterAsync(client, $"share_a_{Guid.NewGuid():N}@test.local")).User.Id);
        var bob = Guid.Parse((await TestHelpers.RegisterAsync(client, $"share_b_{Guid.NewGuid():N}@test.local")).User.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Activities.Add(NewImported(alice, "strava", "777"));
        db.Activities.Add(NewImported(bob, "strava", "777"));

        // Two club-mates sharing one workout is legitimate, not a duplicate.
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Activities.CountAsync(a => a.ExternalId == "777"));
    }

    [Fact]
    public async Task TheSameUser_StillCannotHoldTheSameProviderActivityTwice()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var alice = Guid.Parse((await TestHelpers.RegisterAsync(client, $"dupe_{Guid.NewGuid():N}@test.local")).User.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Activities.Add(NewImported(alice, "strava", "888"));
        await db.SaveChangesAsync();

        db.Activities.Add(NewImported(alice, "strava", "888"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static Activity NewImported(Guid userId, string source, string externalId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = "shared workout",
        ActivityType = "trail",
        GpxFilePath = "x.gpx",
        Source = source,
        ExternalId = externalId,
        Status = ProcessingStatus.Completed,
    };
}
