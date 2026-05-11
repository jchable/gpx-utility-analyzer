namespace GpxAnalyzer.Api.Entities;

public class RacePlanNutritionItem
{
    public Guid Id { get; set; }
    public Guid RacePlanId { get; set; }
    public RacePlan RacePlan { get; set; } = null!;

    // Contexte : pris "au checkpoint" OU "pendant le segment checkpoint A → checkpoint B"
    public Guid? AtCheckpointId { get; set; }
    public Guid? FromCheckpointId { get; set; }
    public Guid? ToCheckpointId { get; set; }

    // Produit (lien soft — ProductName dénormalisé pour survivre à la suppression du produit)
    public Guid? ProductId { get; set; }
    public NutritionProduct? Product { get; set; }
    public string ProductName { get; set; } = "";

    public double Quantity { get; set; } = 1;
    /// <summary>unit | ml | g</summary>
    public string Unit { get; set; } = "unit";

    /// <summary>Timing intra-segment en secondes depuis le début du segment (optionnel)</summary>
    public int? TimeOffsetSeconds { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public RacePlanCheckpoint? AtCheckpoint { get; set; }
    public RacePlanCheckpoint? FromCheckpoint { get; set; }
    public RacePlanCheckpoint? ToCheckpoint { get; set; }
}
