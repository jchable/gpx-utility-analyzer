namespace GpxAnalyzer.Api.Entities;

public class RacePlanCheckpoint
{
    public Guid Id { get; set; }
    public Guid RacePlanId { get; set; }
    public RacePlan RacePlan { get; set; } = null!;

    public int Order { get; set; }
    public string Name { get; set; } = "";
    /// <summary>start | checkpoint | aid_station | crew_only | finish</summary>
    public string Type { get; set; } = "aid_station";

    // Position sur la trace
    public double DistanceKm { get; set; }     // Distance cumulée depuis le départ
    public double? ElevationM { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Timing (secondes depuis le départ)
    public int? CutoffTimeSeconds { get; set; }       // Cutoff officiel de la course
    public int? TargetArrivalSeconds { get; set; }    // Arrivée estimée (calculé automatiquement)
    public int? PlannedPauseSeconds { get; set; }     // Durée de pause planifiée

    // Crew
    public bool IsCrewAccessible { get; set; } = false;
    public string? CrewNotes { get; set; }

    // Drop bag
    public bool HasDropBag { get; set; } = false;
    public string? DropBagContentsJson { get; set; }  // JSON: [{item: string, qty: int}]

    // Équipement à ce checkpoint
    public string? EquipmentTakeJson { get; set; }    // Items à récupérer ici
    public string? EquipmentLeaveJson { get; set; }   // Items à laisser ici

    public string? Notes { get; set; }

    // Navigation (nutrition liée à ce checkpoint ou au segment qui en part)
    public ICollection<RacePlanNutritionItem> NutritionAtCheckpoint { get; set; } = [];
    public ICollection<RacePlanNutritionItem> NutritionFromCheckpoint { get; set; } = [];
    public ICollection<RacePlanNutritionItem> NutritionToCheckpoint { get; set; } = [];
}
