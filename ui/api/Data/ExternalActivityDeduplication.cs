namespace GpxAnalyzer.Api.Data;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Startup companion to the <c>IX_Activities_UserId_Source_ExternalId</c> migration.
///
/// The migration deletes the duplicate imported activities that would otherwise make
/// the new unique index unbuildable, keeping the oldest row of each
/// (UserId, Source, ExternalId) group. SQL inside a migration cannot write to the
/// application log, so the rows about to disappear are enumerated and logged here —
/// once, immediately before <c>Database.Migrate()</c> applies that migration.
/// </summary>
public static class ExternalActivityDeduplication
{
    /// <summary>Migration whose Up() performs the deletion.</summary>
    public const string MigrationId =
        "20260901073913_AddProcessingLeasesOAuthStatesAndPerUserExternalActivityUniqueness";

    public static async Task LogRowsAboutToBeRemovedAsync(
        AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            // Nothing to enumerate on a database that has no schema yet, and nothing
            // to do once the migration has already run.
            var applied = await db.Database.GetAppliedMigrationsAsync(ct);
            if (!applied.Any()) return;

            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            if (!pending.Contains(MigrationId)) return;

            // Materialize before grouping: the Status column is a string-converted
            // enum and complex grouped queries over it do not always translate.
            var imported = await db.Activities
                .AsNoTracking()
                .Where(a => a.ExternalId != null)
                .Select(a => new { a.Id, a.UserId, a.Source, a.ExternalId, a.Name, a.CreatedAt })
                .ToListAsync(ct);

            var doomed = imported
                .GroupBy(a => new { a.UserId, a.Source, a.ExternalId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g
                    .OrderBy(a => a.CreatedAt)
                    .ThenBy(a => a.Id)
                    .Skip(1))          // the oldest row of each group is kept
                .ToList();

            if (doomed.Count == 0) return;

            logger.LogWarning(
                "Migrating to a per-user unique index on imported activities: removing {Count} " +
                "duplicate row(s). The oldest row of each (user, provider, external id) is kept.",
                doomed.Count);

            foreach (var a in doomed)
                logger.LogWarning(
                    "  removing duplicate activity {Id} (\"{Name}\") — user {UserId}, " +
                    "provider {Source}, external id {ExternalId}, created {CreatedAt:u}",
                    a.Id, a.Name, a.UserId, a.Source, a.ExternalId, a.CreatedAt);
        }
        catch (Exception ex)
        {
            // Purely informational: never let the logging pass block the migration.
            logger.LogWarning(ex, "Could not enumerate duplicate imported activities before migrating");
        }
    }
}
