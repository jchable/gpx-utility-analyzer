namespace GpxAnalyzer.Api.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;
using Microsoft.EntityFrameworkCore;

public class RacePlanService
{
    private const int ProfileTargetPoints = 500;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<RacePlanService> _logger;

    public RacePlanService(AppDbContext db, ILogger<RacePlanService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    // List / Get
    // ─────────────────────────────────────────────

    public async Task<List<RacePlanListDto>> ListAsync(
        Guid userId, int page, int pageSize, string? type, string? status,
        CancellationToken ct = default)
    {
        IQueryable<RacePlan> query = _db.RacePlans
            .Where(r => r.UserId == userId)
            .Include(r => r.Checkpoints);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.ActivityType == type);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        var plans = await query
            .OrderByDescending(r => r.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return plans.Select(r => ToListDto(r)).ToList();
    }

    public async Task<RacePlanDetailDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var plan = await LoadFullPlan(userId, id, ct);
        return plan is null ? null : ToDetailDto(plan, includePoints: true);
    }

    public async Task<RacePlanSharedDto?> GetSharedAsync(string token, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans
            .Include(r => r.Checkpoints)
            .FirstOrDefaultAsync(r => r.ShareToken == token && r.IsPublic, ct);

        if (plan is null) return null;
        return ToSharedDto(plan);
    }

    // ─────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────

    public async Task<RacePlan> CreateFromRouteAsync(
        Guid userId, Guid routeId, string language, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == routeId && r.UserId == userId, ct)
            ?? throw new InvalidOperationException($"Route {routeId} not found");

        var coords = route.PointsJson is not null
            ? JsonSerializer.Deserialize<double[][]>(route.PointsJson) ?? []
            : [];

        var plan = new RacePlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = $"{route.Name} — Plan",
            ActivityType = route.ActivityType,
            Language = language,
            RouteId = routeId,
            PointsJson = route.PointsJson,
            DistanceKm = route.DistanceKm,
            ElevationGainM = route.ElevationGainM,
            ElevationLossM = route.ElevationLossM,
            MaxElevationM = route.MaxElevationM,
            MinElevationM = route.MinElevationM,
        };

        if (coords.Length > 0)
        {
            // Coordonnées du départ pour suncalc (lat/lon)
            plan.StartLatitude = coords[0][1];
            plan.StartLongitude = coords[0][0];
            plan.ProfileJson = ComputeProfile(coords);
        }

        // Checkpoints par défaut : départ + arrivée
        plan.Checkpoints =
        [
            CreateDefaultCheckpoint(plan.Id, "Départ", "start", 0, coords, 0),
            CreateDefaultCheckpoint(plan.Id, "Arrivée", "finish", 1, coords, plan.DistanceKm),
        ];

        _db.RacePlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("RacePlan created from route {RouteId}: {PlanId} ({Name})",
            routeId, plan.Id, plan.Name);
        return plan;
    }

    public async Task<RacePlan?> CreateFromGpxAsync(
        Guid userId, Stream gpxStream, string filename, string language, CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"raceplan-import-{Guid.NewGuid()}.gpx");
        try
        {
            using (var fs = File.Create(tempFile))
                await gpxStream.CopyToAsync(fs, ct);

            var doc = GpxParser.ParseFile(tempFile);
            var points = doc.AllPoints();
            if (points.Count == 0) return null;

            var coords = points.Select(p => new[] { p.Lon, p.Lat, p.Ele }).ToArray();

            var plan = new RacePlan
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = Path.GetFileNameWithoutExtension(filename),
                ActivityType = "trail",
                Language = language,
                PointsJson = JsonSerializer.Serialize(coords),
            };

            ComputeStatsFromCoords(plan, coords);

            plan.StartLatitude = coords[0][1];
            plan.StartLongitude = coords[0][0];
            plan.ProfileJson = ComputeProfile(coords);

            plan.Checkpoints =
            [
                CreateDefaultCheckpoint(plan.Id, "Départ", "start", 0, coords, 0),
                CreateDefaultCheckpoint(plan.Id, "Arrivée", "finish", 1, coords, plan.DistanceKm),
            ];

            _db.RacePlans.Add(plan);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("RacePlan created from GPX '{File}': {PlanId}", filename, plan.Id);
            return plan;
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // ─────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────

    public async Task<RacePlan?> UpdateAsync(
        Guid userId, Guid id, RacePlanUpdateDto dto, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (plan is null) return null;

        plan.Name = dto.Name;
        plan.Description = dto.Description;
        plan.ActivityType = dto.ActivityType;
        plan.Status = dto.Status;
        plan.RaceDate = dto.RaceDate;
        plan.StartLatitude = dto.StartLatitude;
        plan.StartLongitude = dto.StartLongitude;
        plan.TargetTimeSeconds = dto.TargetTimeSeconds;
        plan.TargetTimeBSeconds = dto.TargetTimeBSeconds;
        plan.TargetTimeCSeconds = dto.TargetTimeCSeconds;
        plan.PerformanceCoefficient = dto.PerformanceCoefficient;
        plan.SweatRateMLPerHour = dto.SweatRateMLPerHour;
        plan.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.StartTime) && TimeSpan.TryParse(dto.StartTime, out var ts))
            plan.StartTime = ts;
        else
            plan.StartTime = null;

        if (dto.Equipment is not null)
            plan.EquipmentJson = JsonSerializer.Serialize(dto.Equipment, JsonOpts);

        await _db.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (plan is null) return false;

        _db.RacePlans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────
    // Checkpoints
    // ─────────────────────────────────────────────

    public async Task<RacePlanCheckpoint?> AddCheckpointAsync(
        Guid userId, Guid planId, RacePlanCheckpointCreateDto dto, CancellationToken ct = default)
    {
        var plan = await LoadFullPlan(userId, planId, ct);
        if (plan is null) return null;

        var coords = GetCoords(plan);
        var (lat, lon, ele) = FindPointAtDistance(coords, dto.DistanceKm);

        var order = plan.Checkpoints.Any()
            ? plan.Checkpoints.Max(c => c.Order) + 1
            : 0;

        var checkpoint = MapToCheckpoint(planId, dto, order, dto.DistanceKm, lat, lon, ele);

        _db.RacePlanCheckpoints.Add(checkpoint);

        // Recompute all checkpoint times after adding
        var allCheckpoints = plan.Checkpoints.ToList();
        allCheckpoints.Add(checkpoint);
        RacePlanTimeCalculationService.ComputeCheckpointTimes(coords, allCheckpoints, plan.PerformanceCoefficient);

        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return checkpoint;
    }

    public async Task<RacePlanCheckpoint?> UpdateCheckpointAsync(
        Guid userId, Guid planId, Guid checkpointId, RacePlanCheckpointUpdateDto dto, CancellationToken ct = default)
    {
        var plan = await LoadFullPlan(userId, planId, ct);
        if (plan is null) return null;

        var checkpoint = plan.Checkpoints.FirstOrDefault(c => c.Id == checkpointId);
        if (checkpoint is null) return null;

        var coords = GetCoords(plan);
        var (lat, lon, ele) = FindPointAtDistance(coords, dto.DistanceKm);

        checkpoint.Name = dto.Name;
        checkpoint.Type = dto.Type;
        checkpoint.DistanceKm = dto.DistanceKm;
        checkpoint.Latitude = lat;
        checkpoint.Longitude = lon;
        checkpoint.ElevationM = ele;
        checkpoint.CutoffTimeSeconds = dto.CutoffTimeSeconds;
        checkpoint.PlannedPauseSeconds = dto.PlannedPauseSeconds;
        checkpoint.IsCrewAccessible = dto.IsCrewAccessible;
        checkpoint.CrewNotes = dto.CrewNotes;
        checkpoint.HasDropBag = dto.HasDropBag;
        checkpoint.Notes = dto.Notes;
        checkpoint.DropBagContentsJson = dto.DropBagContents is not null
            ? JsonSerializer.Serialize(dto.DropBagContents) : null;
        checkpoint.EquipmentTakeJson = dto.EquipmentTake is not null
            ? JsonSerializer.Serialize(dto.EquipmentTake) : null;
        checkpoint.EquipmentLeaveJson = dto.EquipmentLeave is not null
            ? JsonSerializer.Serialize(dto.EquipmentLeave) : null;

        // Recalcul des temps
        RacePlanTimeCalculationService.ComputeCheckpointTimes(
            coords, plan.Checkpoints.ToList(), plan.PerformanceCoefficient);

        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return checkpoint;
    }

    public async Task<bool> DeleteCheckpointAsync(
        Guid userId, Guid planId, Guid checkpointId, CancellationToken ct = default)
    {
        var plan = await LoadFullPlan(userId, planId, ct);
        if (plan is null) return false;

        var checkpoint = plan.Checkpoints.FirstOrDefault(c => c.Id == checkpointId);
        if (checkpoint is null) return false;

        // Impossible de supprimer le start ou le finish
        if (checkpoint.Type is "start" or "finish") return false;

        // Nullifier les références dans les items nutrition avant de supprimer
        var nutritionItems = await _db.RacePlanNutritionItems
            .Where(n => n.AtCheckpointId == checkpointId
                     || n.FromCheckpointId == checkpointId
                     || n.ToCheckpointId == checkpointId)
            .ToListAsync(ct);

        foreach (var item in nutritionItems)
        {
            if (item.AtCheckpointId == checkpointId) item.AtCheckpointId = null;
            if (item.FromCheckpointId == checkpointId) item.FromCheckpointId = null;
            if (item.ToCheckpointId == checkpointId) item.ToCheckpointId = null;
        }

        _db.RacePlanCheckpoints.Remove(checkpoint);

        // Recalcul des temps (sans le checkpoint supprimé)
        var remaining = plan.Checkpoints.Where(c => c.Id != checkpointId).ToList();
        var coords = GetCoords(plan);
        RacePlanTimeCalculationService.ComputeCheckpointTimes(coords, remaining, plan.PerformanceCoefficient);

        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────
    // Recalcul des temps
    // ─────────────────────────────────────────────

    public async Task<bool> ComputeTimesAsync(Guid userId, Guid planId, CancellationToken ct = default)
    {
        var plan = await LoadFullPlan(userId, planId, ct);
        if (plan is null) return false;

        var coords = GetCoords(plan);
        if (coords.Length < 2) return false;

        RacePlanTimeCalculationService.ComputeCheckpointTimes(
            coords, plan.Checkpoints.ToList(), plan.PerformanceCoefficient);

        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────
    // Nutrition
    // ─────────────────────────────────────────────

    public async Task<RacePlanNutritionItem?> AddNutritionItemAsync(
        Guid userId, Guid planId, RacePlanNutritionItemCreateDto dto, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans.FirstOrDefaultAsync(r => r.Id == planId && r.UserId == userId, ct);
        if (plan is null) return null;

        string productName = dto.ProductName ?? "";

        // Si un produit est sélectionné, dénormaliser le nom
        if (dto.ProductId.HasValue)
        {
            var product = await _db.NutritionProducts
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.UserId == userId, ct);
            if (product is not null)
                productName = $"{product.Brand} {product.Name}".Trim();
        }

        var item = new RacePlanNutritionItem
        {
            Id = Guid.NewGuid(),
            RacePlanId = planId,
            AtCheckpointId = dto.AtCheckpointId,
            FromCheckpointId = dto.FromCheckpointId,
            ToCheckpointId = dto.ToCheckpointId,
            ProductId = dto.ProductId,
            ProductName = productName,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            TimeOffsetSeconds = dto.TimeOffsetSeconds,
            Notes = dto.Notes,
        };

        _db.RacePlanNutritionItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<bool> DeleteNutritionItemAsync(
        Guid userId, Guid planId, Guid itemId, CancellationToken ct = default)
    {
        var item = await _db.RacePlanNutritionItems
            .Where(n => n.Id == itemId && n.RacePlanId == planId)
            .FirstOrDefaultAsync(ct);

        if (item is null) return false;

        // Vérifier que le plan appartient à l'utilisateur
        var planExists = await _db.RacePlans.AnyAsync(r => r.Id == planId && r.UserId == userId, ct);
        if (!planExists) return false;

        _db.RacePlanNutritionItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────
    // Partage crew
    // ─────────────────────────────────────────────

    public async Task<string?> EnableShareAsync(Guid userId, Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans.FirstOrDefaultAsync(r => r.Id == planId && r.UserId == userId, ct);
        if (plan is null) return null;

        if (string.IsNullOrEmpty(plan.ShareToken))
            plan.ShareToken = Guid.NewGuid().ToString("N");

        plan.IsPublic = true;
        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return plan.ShareToken;
    }

    public async Task<bool> DisableShareAsync(Guid userId, Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans.FirstOrDefaultAsync(r => r.Id == planId && r.UserId == userId, ct);
        if (plan is null) return false;

        plan.IsPublic = false;
        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─────────────────────────────────────────────
    // Lien activité (post-course)
    // ─────────────────────────────────────────────

    public async Task<bool> LinkActivityAsync(
        Guid userId, Guid planId, Guid activityId, CancellationToken ct = default)
    {
        var plan = await _db.RacePlans.FirstOrDefaultAsync(r => r.Id == planId && r.UserId == userId, ct);
        if (plan is null) return false;

        var activityExists = await _db.Activities.AnyAsync(a => a.Id == activityId && a.UserId == userId, ct);
        if (!activityExists) return false;

        plan.LinkedActivityId = activityId;
        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RacePlanComparisonDto?> GetComparisonAsync(
        Guid userId, Guid planId, CancellationToken ct = default)
    {
        var plan = await LoadFullPlan(userId, planId, ct);
        if (plan?.LinkedActivityId is null) return null;

        var activity = await _db.Activities
            .FirstOrDefaultAsync(a => a.Id == plan.LinkedActivityId && a.UserId == userId, ct);
        if (activity?.ProfileJson is null) return null;

        var profilePoints = JsonSerializer.Deserialize<JsonElement[]>(activity.ProfileJson) ?? [];

        var checkpoints = plan.Checkpoints.OrderBy(c => c.DistanceKm).ToList();
        var results = new List<RacePlanCheckpointComparisonDto>();

        foreach (var cp in checkpoints)
        {
            // Trouver le profile point le plus proche de la distance du checkpoint
            int? actualSeconds = null;
            double minDiff = double.MaxValue;

            foreach (var pt in profilePoints)
            {
                if (!pt.TryGetProperty("distance", out var distProp)) continue;
                double ptDist = distProp.GetDouble();
                double diff = Math.Abs(ptDist - cp.DistanceKm);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    if (pt.TryGetProperty("elapsedTime", out var elapsed))
                        actualSeconds = (int)elapsed.GetDouble();
                }
            }

            results.Add(new RacePlanCheckpointComparisonDto
            {
                CheckpointId = cp.Id,
                CheckpointName = cp.Name,
                DistanceKm = cp.DistanceKm,
                PlannedSeconds = cp.TargetArrivalSeconds,
                ActualSeconds = actualSeconds,
                DeltaSeconds = cp.TargetArrivalSeconds.HasValue && actualSeconds.HasValue
                    ? actualSeconds.Value - cp.TargetArrivalSeconds.Value
                    : null,
            });
        }

        return new RacePlanComparisonDto
        {
            RacePlanId = planId,
            ActivityId = plan.LinkedActivityId.Value,
            Checkpoints = results.ToArray(),
        };
    }

    // ─────────────────────────────────────────────
    // Helpers privés
    // ─────────────────────────────────────────────

    private async Task<RacePlan?> LoadFullPlan(Guid userId, Guid id, CancellationToken ct)
        => await _db.RacePlans
            .Include(r => r.Checkpoints)
            .Include(r => r.NutritionItems)
                .ThenInclude(n => n.Product)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

    private static double[][] GetCoords(RacePlan plan)
        => plan.PointsJson is not null
            ? JsonSerializer.Deserialize<double[][]>(plan.PointsJson) ?? []
            : [];

    private static (double? lat, double? lon, double? ele) FindPointAtDistance(
        double[][] coords, double targetKm)
    {
        if (coords.Length == 0) return (null, null, null);
        if (targetKm <= 0) return (coords[0][1], coords[0][0], coords[0].Length > 2 ? coords[0][2] : null);

        var cumDist = RacePlanTimeCalculationService.ComputeCumulativeDistances(coords);

        int best = 0;
        double bestDiff = Math.Abs(cumDist[0] - targetKm);
        for (int i = 1; i < cumDist.Length; i++)
        {
            double diff = Math.Abs(cumDist[i] - targetKm);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }

        double? eleVal = coords[best].Length > 2 ? coords[best][2] : null;
        return (coords[best][1], coords[best][0], eleVal);
    }

    private static RacePlanCheckpoint CreateDefaultCheckpoint(
        Guid planId, string name, string type, int order, double[][] coords, double distanceKm)
    {
        var (lat, lon, ele) = coords.Length > 0
            ? FindPointAtDistanceStatic(coords, distanceKm)
            : (null, null, (double?)null);

        return new RacePlanCheckpoint
        {
            Id = Guid.NewGuid(),
            RacePlanId = planId,
            Name = name,
            Type = type,
            Order = order,
            DistanceKm = distanceKm,
            Latitude = lat,
            Longitude = lon,
            ElevationM = ele,
            TargetArrivalSeconds = type == "start" ? 0 : null,
        };
    }

    private static (double? lat, double? lon, double? ele) FindPointAtDistanceStatic(
        double[][] coords, double targetKm)
    {
        if (coords.Length == 0) return (null, null, null);
        if (targetKm <= 0) return (coords[0][1], coords[0][0], coords[0].Length > 2 ? coords[0][2] : null);

        var cumDist = RacePlanTimeCalculationService.ComputeCumulativeDistances(coords);
        double maxDist = cumDist[^1];

        if (targetKm >= maxDist)
        {
            int last = coords.Length - 1;
            return (coords[last][1], coords[last][0], coords[last].Length > 2 ? coords[last][2] : null);
        }

        int best = 0;
        double bestDiff = Math.Abs(cumDist[0] - targetKm);
        for (int i = 1; i < cumDist.Length; i++)
        {
            double diff = Math.Abs(cumDist[i] - targetKm);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }

        return (coords[best][1], coords[best][0], coords[best].Length > 2 ? coords[best][2] : null);
    }

    private static RacePlanCheckpoint MapToCheckpoint(
        Guid planId, RacePlanCheckpointCreateDto dto, int order,
        double distanceKm, double? lat, double? lon, double? ele)
        => new()
        {
            Id = Guid.NewGuid(),
            RacePlanId = planId,
            Order = order,
            Name = dto.Name,
            Type = dto.Type,
            DistanceKm = distanceKm,
            Latitude = lat,
            Longitude = lon,
            ElevationM = ele,
            CutoffTimeSeconds = dto.CutoffTimeSeconds,
            PlannedPauseSeconds = dto.PlannedPauseSeconds,
            IsCrewAccessible = dto.IsCrewAccessible,
            CrewNotes = dto.CrewNotes,
            HasDropBag = dto.HasDropBag,
            Notes = dto.Notes,
            DropBagContentsJson = dto.DropBagContents is not null
                ? JsonSerializer.Serialize(dto.DropBagContents) : null,
            EquipmentTakeJson = dto.EquipmentTake is not null
                ? JsonSerializer.Serialize(dto.EquipmentTake) : null,
            EquipmentLeaveJson = dto.EquipmentLeave is not null
                ? JsonSerializer.Serialize(dto.EquipmentLeave) : null,
        };

    private static void ComputeStatsFromCoords(RacePlan plan, double[][] coords)
    {
        double totalDist = 0, elevGain = 0, elevLoss = 0;
        double maxEle = double.MinValue, minEle = double.MaxValue;

        for (int i = 0; i < coords.Length; i++)
        {
            double ele = coords[i].Length > 2 ? coords[i][2] : 0;
            if (ele > maxEle) maxEle = ele;
            if (ele < minEle) minEle = ele;

            if (i > 0)
            {
                totalDist += DistanceCalculator.Haversine(
                    coords[i - 1][1], coords[i - 1][0],
                    coords[i][1], coords[i][0]);

                double prevEle = coords[i - 1].Length > 2 ? coords[i - 1][2] : 0;
                double dEle = ele - prevEle;
                if (dEle > 2.0) elevGain += dEle;
                else if (dEle < -2.0) elevLoss += Math.Abs(dEle);
            }
        }

        plan.DistanceKm = totalDist / 1000.0;
        plan.ElevationGainM = elevGain;
        plan.ElevationLossM = elevLoss;
        plan.MaxElevationM = maxEle == double.MinValue ? 0 : maxEle;
        plan.MinElevationM = minEle == double.MaxValue ? 0 : minEle;
    }

    /// <summary>
    /// Calcule un profil 500 points (distance, elevation, grade, toblerSpeed)
    /// à partir des coordonnées brutes [lon, lat, ele][].
    /// </summary>
    private static string ComputeProfile(double[][] coords)
    {
        if (coords.Length < 2) return "[]";

        var cumDist = RacePlanTimeCalculationService.ComputeCumulativeDistances(coords);
        double totalKm = cumDist[^1];

        int n = Math.Min(coords.Length, ProfileTargetPoints);
        double step = totalKm / (n - 1);

        var profile = new List<object>(n);

        for (int i = 0; i < n; i++)
        {
            double targetDist = i * step;
            int idx = FindClosestIndex(cumDist, targetDist);

            double ele = coords[idx].Length > 2 ? coords[idx][2] : 0;
            double grade = 0;
            double toblerSpeed = RacePlanTimeCalculationService.ToblerSpeed(0);

            if (idx > 0)
            {
                double dDist = (cumDist[idx] - cumDist[idx - 1]) * 1000; // m
                if (dDist > 0.1)
                {
                    double prevEle = coords[idx - 1].Length > 2 ? coords[idx - 1][2] : 0;
                    double slopeFraction = (ele - prevEle) / dDist;
                    grade = Math.Round(slopeFraction * 100, 1); // en pourcentage
                    toblerSpeed = Math.Round(RacePlanTimeCalculationService.ToblerSpeed(slopeFraction), 2);
                }
            }

            profile.Add(new
            {
                distance = Math.Round(targetDist, 3),
                elevation = Math.Round(ele, 1),
                grade,
                toblerSpeed,
            });
        }

        return JsonSerializer.Serialize(profile, JsonOpts);
    }

    private static int FindClosestIndex(double[] cumDist, double targetKm)
    {
        int best = 0;
        double bestDiff = Math.Abs(cumDist[0] - targetKm);
        for (int i = 1; i < cumDist.Length; i++)
        {
            double diff = Math.Abs(cumDist[i] - targetKm);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    // ─────────────────────────────────────────────
    // DTO Mapping
    // ─────────────────────────────────────────────

    private static RacePlanListDto ToListDto(RacePlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Description = plan.Description,
        ActivityType = plan.ActivityType,
        Status = plan.Status,
        DistanceKm = plan.DistanceKm,
        ElevationGainM = plan.ElevationGainM,
        ElevationLossM = plan.ElevationLossM,
        RaceDate = plan.RaceDate,
        StartTime = plan.StartTime?.ToString(@"HH\:mm"),
        TargetTimeSeconds = plan.TargetTimeSeconds,
        TargetTimeBSeconds = plan.TargetTimeBSeconds,
        TargetTimeCSeconds = plan.TargetTimeCSeconds,
        PerformanceCoefficient = plan.PerformanceCoefficient,
        CheckpointCount = plan.Checkpoints.Count,
        IsPublic = plan.IsPublic,
        LinkedActivityId = plan.LinkedActivityId,
        CreatedAt = plan.CreatedAt,
        UpdatedAt = plan.UpdatedAt,
    };

    private static RacePlanDetailDto ToDetailDto(RacePlan plan, bool includePoints = false)
    {
        RacePlanEquipmentItemDto[]? equipment = null;
        if (!string.IsNullOrEmpty(plan.EquipmentJson))
        {
            try { equipment = JsonSerializer.Deserialize<RacePlanEquipmentItemDto[]>(plan.EquipmentJson, JsonOpts); }
            catch { /* ignore */ }
        }

        object? profile = null;
        if (!string.IsNullOrEmpty(plan.ProfileJson))
        {
            try { profile = JsonSerializer.Deserialize<object>(plan.ProfileJson); }
            catch { /* ignore */ }
        }

        double[][]? points = null;
        if (includePoints && !string.IsNullOrEmpty(plan.PointsJson))
        {
            try { points = JsonSerializer.Deserialize<double[][]>(plan.PointsJson); }
            catch { /* ignore */ }
        }

        return new RacePlanDetailDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            ActivityType = plan.ActivityType,
            Status = plan.Status,
            Language = plan.Language,
            RouteId = plan.RouteId,
            DistanceKm = plan.DistanceKm,
            ElevationGainM = plan.ElevationGainM,
            ElevationLossM = plan.ElevationLossM,
            MaxElevationM = plan.MaxElevationM,
            MinElevationM = plan.MinElevationM,
            RaceDate = plan.RaceDate,
            StartTime = plan.StartTime?.ToString(@"HH\:mm"),
            StartLatitude = plan.StartLatitude,
            StartLongitude = plan.StartLongitude,
            TargetTimeSeconds = plan.TargetTimeSeconds,
            TargetTimeBSeconds = plan.TargetTimeBSeconds,
            TargetTimeCSeconds = plan.TargetTimeCSeconds,
            PerformanceCoefficient = plan.PerformanceCoefficient,
            SweatRateMLPerHour = plan.SweatRateMLPerHour,
            Equipment = equipment,
            IsPublic = plan.IsPublic,
            ShareToken = plan.IsPublic ? plan.ShareToken : null,
            LinkedActivityId = plan.LinkedActivityId,
            Checkpoints = plan.Checkpoints
                .OrderBy(c => c.Order)
                .ThenBy(c => c.DistanceKm)
                .Select(MapCheckpointToDto)
                .ToArray(),
            NutritionItems = plan.NutritionItems
                .Select(MapNutritionItemToDto)
                .ToArray(),
            Profile = profile,
            Points = points,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
        };
    }

    private static RacePlanSharedDto ToSharedDto(RacePlan plan)
    {
        object? profile = null;
        if (!string.IsNullOrEmpty(plan.ProfileJson))
        {
            try { profile = JsonSerializer.Deserialize<object>(plan.ProfileJson); }
            catch { /* ignore */ }
        }

        double[][]? points = null;
        if (!string.IsNullOrEmpty(plan.PointsJson))
        {
            try { points = JsonSerializer.Deserialize<double[][]>(plan.PointsJson); }
            catch { /* ignore */ }
        }

        return new RacePlanSharedDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            ActivityType = plan.ActivityType,
            DistanceKm = plan.DistanceKm,
            ElevationGainM = plan.ElevationGainM,
            ElevationLossM = plan.ElevationLossM,
            RaceDate = plan.RaceDate,
            StartTime = plan.StartTime?.ToString(@"HH\:mm"),
            TargetTimeSeconds = plan.TargetTimeSeconds,
            TargetTimeBSeconds = plan.TargetTimeBSeconds,
            TargetTimeCSeconds = plan.TargetTimeCSeconds,
            Checkpoints = plan.Checkpoints
                .OrderBy(c => c.Order)
                .ThenBy(c => c.DistanceKm)
                .Select(cp => new RacePlanCheckpointSharedDto
                {
                    Id = cp.Id,
                    Order = cp.Order,
                    Name = cp.Name,
                    Type = cp.Type,
                    DistanceKm = cp.DistanceKm,
                    ElevationM = cp.ElevationM,
                    Latitude = cp.Latitude,
                    Longitude = cp.Longitude,
                    CutoffTimeSeconds = cp.CutoffTimeSeconds,
                    TargetArrivalSeconds = cp.TargetArrivalSeconds,
                    PlannedPauseSeconds = cp.PlannedPauseSeconds,
                    IsCrewAccessible = cp.IsCrewAccessible,
                    CrewNotes = cp.CrewNotes,
                })
                .ToArray(),
            Profile = profile,
            Points = points,
        };
    }

    private static RacePlanNutritionItemDto MapNutritionItemToDto(RacePlanNutritionItem item)
    {
        return new RacePlanNutritionItemDto
        {
            Id = item.Id,
            AtCheckpointId = item.AtCheckpointId,
            FromCheckpointId = item.FromCheckpointId,
            ToCheckpointId = item.ToCheckpointId,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            CaloriesKcal = item.Product?.CaloriesKcal,
            CarbsG = item.Product?.CarbsG,
            SodiumMg = item.Product?.SodiumMg,
            Quantity = item.Quantity,
            Unit = item.Unit,
            TimeOffsetSeconds = item.TimeOffsetSeconds,
            Notes = item.Notes,
        };
    }

    private static RacePlanCheckpointDto MapCheckpointToDto(RacePlanCheckpoint cp)
    {
        DropBagItemDto[]? dropBag = null;
        if (!string.IsNullOrEmpty(cp.DropBagContentsJson))
        {
            try { dropBag = JsonSerializer.Deserialize<DropBagItemDto[]>(cp.DropBagContentsJson); }
            catch { /* ignore */ }
        }

        string[]? equipTake = null, equipLeave = null;
        if (!string.IsNullOrEmpty(cp.EquipmentTakeJson))
        {
            try { equipTake = JsonSerializer.Deserialize<string[]>(cp.EquipmentTakeJson); }
            catch { /* ignore */ }
        }
        if (!string.IsNullOrEmpty(cp.EquipmentLeaveJson))
        {
            try { equipLeave = JsonSerializer.Deserialize<string[]>(cp.EquipmentLeaveJson); }
            catch { /* ignore */ }
        }

        return new RacePlanCheckpointDto
        {
            Id = cp.Id,
            Order = cp.Order,
            Name = cp.Name,
            Type = cp.Type,
            DistanceKm = cp.DistanceKm,
            ElevationM = cp.ElevationM,
            Latitude = cp.Latitude,
            Longitude = cp.Longitude,
            CutoffTimeSeconds = cp.CutoffTimeSeconds,
            TargetArrivalSeconds = cp.TargetArrivalSeconds,
            PlannedPauseSeconds = cp.PlannedPauseSeconds,
            IsCrewAccessible = cp.IsCrewAccessible,
            CrewNotes = cp.CrewNotes,
            HasDropBag = cp.HasDropBag,
            DropBagContents = dropBag,
            EquipmentTake = equipTake,
            EquipmentLeave = equipLeave,
            Notes = cp.Notes,
        };
    }
}
