using System.Text.Json;
using System.Text.RegularExpressions;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Output;

public sealed class JsonFormatter : IFormatter
{
    private readonly JsonSerializerOptions? _options;

    public JsonFormatter(JsonSerializerOptions? options = null)
    {
        _options = options;
    }

    public void Format(TextWriter writer, string filename, Summary s, StopConfig config)
    {
        var js = new JsonSummary
        {
            Filename = filename,
            TotalDistanceM = s.TotalDistance,
            TotalDistance3dM = s.TotalDistance3D,
            TotalDistanceKm = s.TotalDistance / 1000,
            ElevationGainM = s.Elevation.Gain,
            ElevationLossM = s.Elevation.Loss,
            MaxElevationM = s.Elevation.Max,
            MinElevationM = s.Elevation.Min,
            StartTime = s.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            EndTime = s.EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            TotalTime = ToDur(s.TotalTime),
            MovingTime = ToDur(s.MovingTime),
            StoppedTime = ToDur(s.StoppedTime),
            AvgSpeedKmh = s.Speed.AvgSpeed * 3.6,
            AvgMovingSpeedKmh = s.Speed.AvgMovingSpeed * 3.6,
            MaxSpeedKmh = s.Speed.MaxSpeed * 3.6,
            AvgPace = FormatHelpers.FormatPace(s.Speed.AvgPace),
            AvgMovingPace = FormatHelpers.FormatPace(s.Speed.AvgMovingPace),
            PointCount = s.PointCount,
            FilteredPoints = s.FilteredPoints,
            SegmentCount = s.SegmentCount,
            PointsPerKm = s.PointsPerKm,
            StopCount = s.StopCount,
            TotalStopTime = ToDur(s.TotalStopTime),
            AvgStopDuration = ToDur(s.AvgStopDuration),
            LongestStop = s.LongestStop != null ? ToJsonStop(s.LongestStop) : null,
            Stops = s.Stops.Count > 0 ? s.Stops.Select(ToJsonStop).ToList() : null,
            HeartRate = MapHeartRate(s.Biometrics.HeartRate),
            Power = MapPower(s.Biometrics.Power),
            Cadence = MapCadence(s.Biometrics.Cadence),
            Temperature = MapTemperature(s.Biometrics.Temperature),
            Effort = MapEffort(s.Effort),
            Anomalies = MapAnomalyReport(s.AnomalyReport),
        };

        var opts = _options ?? new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(js, opts);
        writer.WriteLine(json);
    }

    private static JsonDuration ToDur(TimeSpan d) => new()
    {
        Display = FormatHelpers.FormatDuration(d),
        Seconds = d.TotalSeconds
    };

    private static JsonStop ToJsonStop(Stop s) => new()
    {
        StartTime = s.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        EndTime = s.EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Duration = ToDur(s.Duration),
        Lat = s.Lat,
        Lon = s.Lon
    };

    private static JsonHeartRate? MapHeartRate(HeartRateResult? hr)
    {
        if (hr == null) return null;
        return new JsonHeartRate
        {
            AvgBpm = hr.Avg,
            MaxBpm = hr.Max,
            MinBpm = hr.Min,
            Zones = hr.Zones.Count > 0 ? hr.Zones.Select(z => new JsonHRZone
            {
                Name = z.Name,
                MinPercent = z.MinPercent,
                MaxPercent = z.MaxPercent,
                Duration = ToDur(z.Duration)
            }).ToList() : null
        };
    }

    private static JsonPower? MapPower(PowerResult? pw)
    {
        if (pw == null) return null;
        return new JsonPower
        {
            AvgWatts = pw.Avg,
            MaxWatts = pw.Max,
            NormalizedPowerWatts = pw.NormalizedPower
        };
    }

    private static JsonCadence? MapCadence(CadenceResult? cad)
    {
        if (cad == null) return null;
        return new JsonCadence { AvgRpm = cad.Avg, MaxRpm = cad.Max };
    }

    private static JsonTemperature? MapTemperature(TemperatureResult? temp)
    {
        if (temp == null) return null;
        return new JsonTemperature { AvgCelsius = temp.Avg, MinCelsius = temp.Min, MaxCelsius = temp.Max };
    }

    private static JsonAnomalyReport? MapAnomalyReport(AnomalyReport? r)
    {
        if (r == null || r.IsClean) return null;
        return new JsonAnomalyReport
        {
            QualityScore = r.QualityScore,
            TotalCount = r.TotalCount,
            InfoCount = r.InfoCount,
            WarningCount = r.WarningCount,
            CriticalCount = r.CriticalCount,
            DistanceImpactM = r.TotalDistanceImpactM,
            TimeImpactS = r.TotalTimeImpactS,
            CorrectionApplied = r.CorrectionApplied,
            Anomalies = r.Anomalies.Select(a => new JsonAnomaly
            {
                Type = ToSnakeCase(a.Type.ToString()),
                Category = ToSnakeCase(a.Category.ToString()),
                Severity = ToSnakeCase(a.Severity.ToString()),
                StartIndex = a.StartIndex,
                EndIndex = a.EndIndex,
                StartTime = a.StartTime?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                EndTime = a.EndTime?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                DistanceImpactM = a.DistanceImpactM,
                TimeImpactS = a.TimeImpactS,
                Description = a.Description,
                WasCorrected = a.WasCorrected,
            }).ToList(),
        };
    }

    internal static string ToSnakeCase(string name) =>
        Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    private static JsonEffortMetrics MapEffort(EffortMetrics e) => new()
    {
        NaismithTime = ToDur(e.NaismithTime),
        ToblerTime = ToDur(e.ToblerTime),
        MunterTime = ToDur(e.MunterTime),
        PerformanceRatioNaismith = e.PerformanceRatioNaismith,
        PerformanceRatioTobler = e.PerformanceRatioTobler,
        KilometreEffort = e.KilometreEffort,
        ItraPoints = e.ItraPoints,
        ItraCategory = e.ItraCategory,
        EquivalentFlatDistanceKm = e.EquivalentFlatDistanceKm,
        TerrainDifficulty = new JsonTerrainDifficulty
        {
            Score = e.TerrainDifficulty.Score,
            Grade = e.TerrainDifficulty.Grade,
            AvgGradePercent = e.TerrainDifficulty.AvgGradePercent,
            MaxGradePercent = e.TerrainDifficulty.MaxGradePercent,
            GradeVariance = e.TerrainDifficulty.GradeVariance,
            SteepSectionRatio = e.TerrainDifficulty.SteepSectionRatio,
            ElevationPerKm = e.TerrainDifficulty.ElevationPerKm,
        },
    };
}
