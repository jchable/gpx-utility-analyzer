namespace GpxAnalyzer.Api.Services;

using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using Microsoft.EntityFrameworkCore;

public class NutritionProductService
{
    private readonly AppDbContext _db;

    public NutritionProductService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<NutritionProductDto>> ListAsync(
        Guid userId, string? type, CancellationToken ct = default)
    {
        var query = _db.NutritionProducts.Where(p => p.UserId == userId);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(p => p.Type == type);

        return await query
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Brand)
            .ThenBy(p => p.Name)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
    }

    public async Task<NutritionProductDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var p = await _db.NutritionProducts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<NutritionProductDto> CreateAsync(
        Guid userId, NutritionProductCreateDto dto, CancellationToken ct = default)
    {
        var product = new NutritionProduct
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Brand = dto.Brand,
            Type = dto.Type,
            CaloriesKcal = dto.CaloriesKcal,
            CarbsG = dto.CarbsG,
            ProteinsG = dto.ProteinsG,
            FatsG = dto.FatsG,
            SodiumMg = dto.SodiumMg,
            CaffeineG = dto.CaffeineG,
            WeightG = dto.WeightG,
            VolumeML = dto.VolumeML,
            Notes = dto.Notes,
        };

        _db.NutritionProducts.Add(product);
        await _db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<NutritionProductDto?> UpdateAsync(
        Guid userId, Guid id, NutritionProductUpdateDto dto, CancellationToken ct = default)
    {
        var product = await _db.NutritionProducts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (product is null) return null;

        product.Name = dto.Name;
        product.Brand = dto.Brand;
        product.Type = dto.Type;
        product.CaloriesKcal = dto.CaloriesKcal;
        product.CarbsG = dto.CarbsG;
        product.ProteinsG = dto.ProteinsG;
        product.FatsG = dto.FatsG;
        product.SodiumMg = dto.SodiumMg;
        product.CaffeineG = dto.CaffeineG;
        product.WeightG = dto.WeightG;
        product.VolumeML = dto.VolumeML;
        product.Notes = dto.Notes;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var product = await _db.NutritionProducts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (product is null) return false;

        _db.NutritionProducts.Remove(product);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> ImportDefaultsAsync(Guid userId, CancellationToken ct = default)
    {
        var hasAny = await _db.NutritionProducts.AnyAsync(p => p.UserId == userId, ct);
        if (hasAny) return 0;

        var now = DateTime.UtcNow;

        var defaults = new List<NutritionProduct>
        {
            // Gels
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "GO Energy Gel", Brand = "SIS", Type = "gel", CaloriesKcal = 88, CarbsG = 22, ProteinsG = 0, FatsG = 0, SodiumMg = 30, WeightG = 60, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "GO Energy Gel Caféine", Brand = "SIS", Type = "gel", CaloriesKcal = 87, CarbsG = 22, ProteinsG = 0, FatsG = 0, SodiumMg = 30, CaffeineG = 0.075, WeightG = 60, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Gel 100", Brand = "Maurten", Type = "gel", CaloriesKcal = 100, CarbsG = 25, ProteinsG = 0, FatsG = 0, SodiumMg = 55, WeightG = 40, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Gel 100 CAF 100", Brand = "Maurten", Type = "gel", CaloriesKcal = 100, CarbsG = 25, ProteinsG = 0, FatsG = 0, SodiumMg = 55, CaffeineG = 0.1, WeightG = 40, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Roctane Energy Gel", Brand = "GU", Type = "gel", CaloriesKcal = 100, CarbsG = 21, ProteinsG = 1, FatsG = 2, SodiumMg = 125, WeightG = 32, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Energy Gel", Brand = "Clif Shot", Type = "gel", CaloriesKcal = 100, CarbsG = 24, ProteinsG = 0, FatsG = 0, SodiumMg = 50, WeightG = 34, CreatedAt = now, UpdatedAt = now },
            // Barres
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Energy Bar", Brand = "Clif", Type = "bar", CaloriesKcal = 250, CarbsG = 45, ProteinsG = 9, FatsG = 5, SodiumMg = 150, WeightG = 68, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Bar Dates & Chocolate", Brand = "Chimpanzee", Type = "bar", CaloriesKcal = 194, CarbsG = 31, ProteinsG = 5, FatsG = 6, SodiumMg = 30, WeightG = 55, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Race Bar", Brand = "Maurten", Type = "bar", CaloriesKcal = 225, CarbsG = 50, ProteinsG = 3, FatsG = 3, SodiumMg = 130, WeightG = 70, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Natural Energy Bar", Brand = "SIS", Type = "bar", CaloriesKcal = 185, CarbsG = 26, ProteinsG = 5, FatsG = 7, SodiumMg = 80, WeightG = 40, CreatedAt = now, UpdatedAt = now },
            // Boissons
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Drink Mix 160", Brand = "Maurten", Type = "drink", CaloriesKcal = 160, CarbsG = 40, ProteinsG = 0, FatsG = 0, SodiumMg = 55, VolumeML = 500, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "GO Electrolyte", Brand = "SIS", Type = "drink", CaloriesKcal = 136, CarbsG = 36, ProteinsG = 0, FatsG = 0, SodiumMg = 410, VolumeML = 500, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Roctane Drink Mix", Brand = "GU", Type = "drink", CaloriesKcal = 280, CarbsG = 68, ProteinsG = 0, FatsG = 0, SodiumMg = 320, VolumeML = 500, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Isotonic Energy", Brand = "Decathlon", Type = "drink", CaloriesKcal = 150, CarbsG = 37, ProteinsG = 0, FatsG = 0, SodiumMg = 420, VolumeML = 500, CreatedAt = now, UpdatedAt = now },
            // Électrolytes
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Hydration Tab", Brand = "SIS", Type = "electrolyte", CaloriesKcal = 17, CarbsG = 4, ProteinsG = 0, FatsG = 0, SodiumMg = 520, VolumeML = 500, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Nuun Sport", Brand = "Nuun", Type = "electrolyte", CaloriesKcal = 15, CarbsG = 4, ProteinsG = 0, FatsG = 0, SodiumMg = 360, VolumeML = 500, CreatedAt = now, UpdatedAt = now },
            // Aliments naturels
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Banane", Brand = null, Type = "real_food", CaloriesKcal = 89, CarbsG = 23, ProteinsG = 1, FatsG = 0, SodiumMg = 1, WeightG = 100, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Bouillon (bol)", Brand = null, Type = "real_food", CaloriesKcal = 15, CarbsG = 1, ProteinsG = 2, FatsG = 0, SodiumMg = 750, VolumeML = 200, CreatedAt = now, UpdatedAt = now },
        };

        _db.NutritionProducts.AddRange(defaults);
        await _db.SaveChangesAsync(ct);
        return defaults.Count;
    }

    private static NutritionProductDto ToDto(NutritionProduct p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Brand = p.Brand,
        Type = p.Type,
        CaloriesKcal = p.CaloriesKcal,
        CarbsG = p.CarbsG,
        ProteinsG = p.ProteinsG,
        FatsG = p.FatsG,
        SodiumMg = p.SodiumMg,
        CaffeineG = p.CaffeineG,
        WeightG = p.WeightG,
        VolumeML = p.VolumeML,
        Notes = p.Notes,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };
}
