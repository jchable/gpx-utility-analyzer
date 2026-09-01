using GpxAnalyzer.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// Disambiguate from Microsoft.AspNetCore.Routing.Route (pulled in by implicit usings).
using Route = GpxAnalyzer.Api.Entities.Route;

namespace GpxAnalyzer.Api.Data;

/// <summary>
/// EF Core context backing the API. Uses ASP.NET Identity with Guid keys
/// (<see cref="ApplicationUser"/> + <see cref="IdentityRole{Guid}"/>).
/// The relational schema is created/updated from the migrations under
/// <c>Migrations/</c> (applied via <c>Database.Migrate()</c> at startup).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AthleteProfile> AthleteProfiles => Set<AthleteProfile>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RacePlan> RacePlans => Set<RacePlan>();
    public DbSet<RacePlanCheckpoint> RacePlanCheckpoints => Set<RacePlanCheckpoint>();
    public DbSet<RacePlanNutritionItem> RacePlanNutritionItems => Set<RacePlanNutritionItem>();
    public DbSet<NutritionProduct> NutritionProducts => Set<NutritionProduct>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Key-value settings use natural (non-"Id") primary keys.
        builder.Entity<GlobalSetting>().HasKey(g => g.Key);
        builder.Entity<Setting>().HasKey(s => new { s.UserId, s.Key });
        builder.Entity<OAuthState>().HasKey(s => s.Nonce);

        // Activity.Status (ProcessingStatus enum) is persisted as its string name.
        builder.Entity<Activity>()
            .Property(a => a.Status)
            .HasConversion<string>();

        // One integration per (user, provider).
        builder.Entity<Integration>()
            .HasIndex(i => new { i.UserId, i.Provider })
            .IsUnique();

        // NutritionProduct has no User navigation, so bind the collection to the
        // existing UserId FK explicitly (otherwise EF invents a shadow FK).
        builder.Entity<NutritionProduct>(e =>
        {
            e.HasOne<ApplicationUser>()
                .WithMany(u => u.NutritionProducts)
                .HasForeignKey(np => np.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(np => new { np.UserId, np.Type });
        });

        // Query indexes (match the schema created by the migrations).
        builder.Entity<Activity>(e =>
        {
            e.HasIndex(a => a.StartTime);
            e.HasIndex(a => a.Status);
            // Scoped per user on purpose: two athletes who shared the same workout
            // both legitimately hold the provider's activity id. Only re-importing
            // it for the SAME user is a duplicate.
            e.HasIndex(a => new { a.UserId, a.Source, a.ExternalId }).IsUnique();
            e.HasIndex(a => new { a.UserId, a.StartTime });
        });
        builder.Entity<Route>(e =>
        {
            e.HasIndex(r => r.ActivityType);
            e.HasIndex(r => r.CreatedAt);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => new { r.UserId, r.UpdatedAt });
        });
        builder.Entity<RacePlan>(e =>
        {
            e.HasIndex(r => r.CreatedAt);
            e.HasIndex(r => r.ShareToken);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => new { r.UserId, r.UpdatedAt });
        });
        builder.Entity<RacePlanCheckpoint>()
            .HasIndex(c => new { c.RacePlanId, c.Order });
        builder.Entity<RefreshToken>()
            .HasIndex(t => t.Token);

        // RacePlanNutritionItem has three distinct optional links to a checkpoint
        // (at / from / to), so the relationships must be configured explicitly —
        // EF cannot disambiguate multiple relationships between the same types.
        builder.Entity<RacePlanNutritionItem>(e =>
        {
            e.HasOne(n => n.AtCheckpoint)
                .WithMany(c => c.NutritionAtCheckpoint)
                .HasForeignKey(n => n.AtCheckpointId);

            e.HasOne(n => n.FromCheckpoint)
                .WithMany(c => c.NutritionFromCheckpoint)
                .HasForeignKey(n => n.FromCheckpointId);

            e.HasOne(n => n.ToCheckpoint)
                .WithMany(c => c.NutritionToCheckpoint)
                .HasForeignKey(n => n.ToCheckpointId);
        });
    }
}
