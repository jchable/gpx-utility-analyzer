namespace GpxAnalyzer.Api.Dto;

using System.Text.Json.Serialization;

// ─────────────────────────────────────────────
// Race Plan — list & detail
// ─────────────────────────────────────────────

public class RacePlanListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "";
    public string Status { get; set; } = "";
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public DateTime? RaceDate { get; set; }
    public string? StartTime { get; set; }       // "HH:mm" formatted
    public int? TargetTimeSeconds { get; set; }
    public int? TargetTimeBSeconds { get; set; }
    public int? TargetTimeCSeconds { get; set; }
    public double PerformanceCoefficient { get; set; }
    public int CheckpointCount { get; set; }
    public bool IsPublic { get; set; }
    public Guid? LinkedActivityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RacePlanDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "";
    public string Status { get; set; } = "";
    public string Language { get; set; } = "en";
    public Guid? RouteId { get; set; }

    // Stats trace
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public double MaxElevationM { get; set; }
    public double MinElevationM { get; set; }

    // Race details
    public DateTime? RaceDate { get; set; }
    public string? StartTime { get; set; }       // "HH:mm" formatted
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }

    // Objectifs
    public int? TargetTimeSeconds { get; set; }
    public int? TargetTimeBSeconds { get; set; }
    public int? TargetTimeCSeconds { get; set; }
    public double PerformanceCoefficient { get; set; }
    public double? SweatRateMLPerHour { get; set; }

    // Équipement
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RacePlanEquipmentItemDto[]? Equipment { get; set; }

    // Partage
    public bool IsPublic { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShareToken { get; set; }

    // Post-course
    public Guid? LinkedActivityId { get; set; }

    // Checkpoints (triés par Order)
    public RacePlanCheckpointDto[] Checkpoints { get; set; } = [];

    // Nutrition items
    public RacePlanNutritionItemDto[] NutritionItems { get; set; } = [];

    // Profil (500 pts)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Profile { get; set; }

    // Points bruts pour la carte
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[][]? Points { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Vue partagée (crew) — informations réduites, sans données privées
public class RacePlanSharedDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "";
    public double DistanceKm { get; set; }
    public double ElevationGainM { get; set; }
    public double ElevationLossM { get; set; }
    public DateTime? RaceDate { get; set; }
    public string? StartTime { get; set; }
    public int? TargetTimeSeconds { get; set; }
    public int? TargetTimeBSeconds { get; set; }
    public int? TargetTimeCSeconds { get; set; }
    public RacePlanCheckpointSharedDto[] Checkpoints { get; set; } = [];
    public object? Profile { get; set; }
    public double[][]? Points { get; set; }
}

// ─────────────────────────────────────────────
// Checkpoint
// ─────────────────────────────────────────────

public class RacePlanCheckpointDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public double DistanceKm { get; set; }
    public double? ElevationM { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? CutoffTimeSeconds { get; set; }
    public int? TargetArrivalSeconds { get; set; }
    public int? PlannedPauseSeconds { get; set; }
    public bool IsCrewAccessible { get; set; }
    public string? CrewNotes { get; set; }
    public bool HasDropBag { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DropBagItemDto[]? DropBagContents { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? EquipmentTake { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? EquipmentLeave { get; set; }

    public string? Notes { get; set; }
}

// Version simplifiée pour la vue crew (masque les notes privées)
public class RacePlanCheckpointSharedDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public double DistanceKm { get; set; }
    public double? ElevationM { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? CutoffTimeSeconds { get; set; }
    public int? TargetArrivalSeconds { get; set; }
    public int? PlannedPauseSeconds { get; set; }
    public bool IsCrewAccessible { get; set; }
    public string? CrewNotes { get; set; }
}

// ─────────────────────────────────────────────
// Équipement
// ─────────────────────────────────────────────

public class RacePlanEquipmentItemDto
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "other"; // clothing|footwear|navigation|nutrition|safety|lighting|other
    public bool IsMandatory { get; set; } = false;
    public string? Notes { get; set; }
}

// ─────────────────────────────────────────────
// Drop bag
// ─────────────────────────────────────────────

public class DropBagItemDto
{
    public string Item { get; set; } = "";
    public int Qty { get; set; } = 1;
}

// ─────────────────────────────────────────────
// Nutrition
// ─────────────────────────────────────────────

public class RacePlanNutritionItemDto
{
    public Guid Id { get; set; }
    public Guid? AtCheckpointId { get; set; }
    public Guid? FromCheckpointId { get; set; }
    public Guid? ToCheckpointId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = "";

    // Macros dénormalisées (snapshot au moment de l'ajout)
    public double? CaloriesKcal { get; set; }
    public double? CarbsG { get; set; }
    public double? SodiumMg { get; set; }

    public double Quantity { get; set; }
    public string Unit { get; set; } = "unit";
    public int? TimeOffsetSeconds { get; set; }
    public string? Notes { get; set; }
}

// ─────────────────────────────────────────────
// Nutrition products catalogue
// ─────────────────────────────────────────────

public class NutritionProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public string Type { get; set; } = "";
    public double CaloriesKcal { get; set; }
    public double CarbsG { get; set; }
    public double? ProteinsG { get; set; }
    public double? FatsG { get; set; }
    public double? SodiumMg { get; set; }
    public double? CaffeineG { get; set; }
    public double? WeightG { get; set; }
    public double? VolumeML { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ─────────────────────────────────────────────
// Request DTOs (Create / Update)
// ─────────────────────────────────────────────

public class RacePlanCreateDto
{
    public string? Name { get; set; }
    public string ActivityType { get; set; } = "trail";
    public Guid? RouteId { get; set; }
}

public class RacePlanUpdateDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ActivityType { get; set; } = "trail";
    public string Status { get; set; } = "draft";
    public DateTime? RaceDate { get; set; }
    public string? StartTime { get; set; }           // "HH:mm"
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public int? TargetTimeSeconds { get; set; }
    public int? TargetTimeBSeconds { get; set; }
    public int? TargetTimeCSeconds { get; set; }
    public double PerformanceCoefficient { get; set; } = 0.75;
    public double? SweatRateMLPerHour { get; set; }
    public RacePlanEquipmentItemDto[]? Equipment { get; set; }
}

public class RacePlanCheckpointCreateDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "aid_station";
    public double DistanceKm { get; set; }
    public int? CutoffTimeSeconds { get; set; }
    public int? PlannedPauseSeconds { get; set; }
    public bool IsCrewAccessible { get; set; } = false;
    public string? CrewNotes { get; set; }
    public bool HasDropBag { get; set; } = false;
    public DropBagItemDto[]? DropBagContents { get; set; }
    public string[]? EquipmentTake { get; set; }
    public string[]? EquipmentLeave { get; set; }
    public string? Notes { get; set; }
}

public class RacePlanCheckpointUpdateDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "aid_station";
    public double DistanceKm { get; set; }
    public int? CutoffTimeSeconds { get; set; }
    public int? PlannedPauseSeconds { get; set; }
    public bool IsCrewAccessible { get; set; } = false;
    public string? CrewNotes { get; set; }
    public bool HasDropBag { get; set; } = false;
    public DropBagItemDto[]? DropBagContents { get; set; }
    public string[]? EquipmentTake { get; set; }
    public string[]? EquipmentLeave { get; set; }
    public string? Notes { get; set; }
}

public class RacePlanNutritionItemCreateDto
{
    public Guid? AtCheckpointId { get; set; }
    public Guid? FromCheckpointId { get; set; }
    public Guid? ToCheckpointId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public double Quantity { get; set; } = 1;
    public string Unit { get; set; } = "unit";
    public int? TimeOffsetSeconds { get; set; }
    public string? Notes { get; set; }
}

public class NutritionProductCreateDto
{
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public string Type { get; set; } = "gel";
    public double CaloriesKcal { get; set; }
    public double CarbsG { get; set; }
    public double? ProteinsG { get; set; }
    public double? FatsG { get; set; }
    public double? SodiumMg { get; set; }
    public double? CaffeineG { get; set; }
    public double? WeightG { get; set; }
    public double? VolumeML { get; set; }
    public string? Notes { get; set; }
}

public class NutritionProductUpdateDto
{
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public string Type { get; set; } = "gel";
    public double CaloriesKcal { get; set; }
    public double CarbsG { get; set; }
    public double? ProteinsG { get; set; }
    public double? FatsG { get; set; }
    public double? SodiumMg { get; set; }
    public double? CaffeineG { get; set; }
    public double? WeightG { get; set; }
    public double? VolumeML { get; set; }
    public string? Notes { get; set; }
}

// ─────────────────────────────────────────────
// Comparaison post-course
// ─────────────────────────────────────────────

public class RacePlanComparisonDto
{
    public Guid RacePlanId { get; set; }
    public Guid ActivityId { get; set; }
    public RacePlanCheckpointComparisonDto[] Checkpoints { get; set; } = [];
}

public class RacePlanCheckpointComparisonDto
{
    public Guid CheckpointId { get; set; }
    public string CheckpointName { get; set; } = "";
    public double DistanceKm { get; set; }
    public int? PlannedSeconds { get; set; }
    public int? ActualSeconds { get; set; }
    public int? DeltaSeconds { get; set; }   // Positif = en retard, négatif = en avance
}
