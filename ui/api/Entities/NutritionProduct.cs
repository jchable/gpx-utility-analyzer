namespace GpxAnalyzer.Api.Entities;

public class NutritionProduct
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    /// <summary>gel | bar | drink | real_food | electrolyte | supplement</summary>
    public string Type { get; set; } = "gel";

    // Macros par unité (ou par 100ml/100g pour les boissons en vrac)
    public double CaloriesKcal { get; set; }
    public double CarbsG { get; set; }
    public double? ProteinsG { get; set; }
    public double? FatsG { get; set; }
    public double? SodiumMg { get; set; }
    public double? CaffeineG { get; set; }

    // Conditionnement
    public double? WeightG { get; set; }   // Poids par unité (pour calcul du sac)
    public double? VolumeML { get; set; }  // Volume par unité (boissons)

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
