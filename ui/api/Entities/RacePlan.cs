namespace GpxAnalyzer.Api.Entities;

public class RacePlan
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    // Identité
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "trail"; // trail|run|cycle|hike|walk|other
    public string Status { get; set; } = "draft";        // draft|ready|archived
    public string Language { get; set; } = "en";

    // Source de la trace : Route existante OU upload GPX direct
    public Guid? RouteId { get; set; }
    public string? PointsJson { get; set; }   // [lon, lat, ele][] — copie des points bruts
    public string? ProfileJson { get; set; }  // 500 pts précomputés (distance, elevation, grade, toblerSpeed)

    // Stats trace
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public double MaxElevationM { get; set; }
    public double MinElevationM { get; set; }

    // Détails de la course
    public DateTime? RaceDate { get; set; }          // Date de départ (pour calcul jour/nuit)
    public TimeSpan? StartTime { get; set; }         // Heure de départ (ex: 05:00:00)
    public double? StartLatitude { get; set; }       // Coordonnées du départ pour suncalc
    public double? StartLongitude { get; set; }

    // Objectifs de temps (secondes depuis le départ)
    public int? TargetTimeSeconds { get; set; }      // Objectif A (optimiste)
    public int? TargetTimeBSeconds { get; set; }     // Objectif B (réaliste)
    public int? TargetTimeCSeconds { get; set; }     // Objectif C (prudent)

    // Coefficient de performance (ratio vs Tobler, ex: 0.75 = 75% de la vitesse Tobler théorique)
    public double PerformanceCoefficient { get; set; } = 0.75;

    // Hydratation — taux de transpiration estimé (null = non renseigné, défaut UI: 500 ml/h)
    public double? SweatRateMLPerHour { get; set; }

    // Équipement global (JSON Array<{ name, category, isMandatory, notes }>)
    public string? EquipmentJson { get; set; }

    // Partage crew (lien public read-only)
    public string? ShareToken { get; set; }
    public bool IsPublic { get; set; } = false;

    // Lien post-course pour comparaison
    public Guid? LinkedActivityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<RacePlanCheckpoint> Checkpoints { get; set; } = [];
    public ICollection<RacePlanNutritionItem> NutritionItems { get; set; } = [];
}
