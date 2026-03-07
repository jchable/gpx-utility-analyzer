using System.Text.RegularExpressions;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Stats;
using GpxAiAnalyzer.Core.Models;

namespace GpxAnalyzer.Cli.Core.Output;

/// <summary>
/// Maps the internal <see cref="Summary"/> to the shared <see cref="GpxStats"/> contract model.
/// </summary>
public static class SummaryMapper
{
    public static GpxStats ToGpxStats(string filename, Summary s)
    {
        return new GpxStats
        {
            Filename = Path.GetFileName(filename),
            TotalDistanceM = s.TotalDistance,
            TotalDistance3dM = s.TotalDistance3D,
            TotalDistanceKm = s.TotalDistance / 1000,
            ElevationGainM = s.Elevation.Gain,
            ElevationLossM = s.Elevation.Loss,
            MaxElevationM = s.Elevation.Max,
            MinElevationM = s.Elevation.Min,
            StartTime = s.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            EndTime = s.EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            TotalTime = ToDuration(s.TotalTime),
            MovingTime = ToDuration(s.MovingTime),
            StoppedTime = ToDuration(s.StoppedTime),
            AvgSpeedKmh = s.Speed.AvgSpeed * 3.6,
            AvgMovingSpeedKmh = s.Speed.AvgMovingSpeed * 3.6,
            MaxSpeedKmh = s.Speed.MaxSpeed * 3.6,
            AvgPace = FormatHelpers.FormatPace(s.Speed.AvgPace),
            AvgMovingPace = FormatHelpers.FormatPace(s.Speed.AvgMovingPace),
            PointCount = s.PointCount,
            SegmentCount = s.SegmentCount,
            PointsPerKm = s.PointsPerKm,
            StopCount = s.StopCount,
            TotalStopTime = ToDuration(s.TotalStopTime),
            AvgStopDuration = ToDuration(s.AvgStopDuration),
            LongestStop = s.LongestStop != null ? ToStopInfo(s.LongestStop) : null,
            Stops = s.Stops.Count > 0 ? s.Stops.Select(ToStopInfo).ToList() : null,
            HeartRate = MapHeartRate(s.Biometrics.HeartRate),
            Power = MapPower(s.Biometrics.Power),
            Cadence = MapCadence(s.Biometrics.Cadence),
            Temperature = MapTemperature(s.Biometrics.Temperature),
            Effort = MapEffort(s.Effort),
            Anomalies = MapAnomalyReport(s.AnomalyReport),
        };
    }

    private static DurationValue ToDuration(TimeSpan d) => new()
    {
        Display = FormatHelpers.FormatDuration(d),
        Seconds = d.TotalSeconds
    };

    private static StopInfo ToStopInfo(Stop s) => new()
    {
        StartTime = s.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        EndTime = s.EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Duration = ToDuration(s.Duration),
        Lat = s.Lat,
        Lon = s.Lon,
    };

    private static HeartRateStats? MapHeartRate(HeartRateResult? hr)
    {
        if (hr == null) return null;
        return new HeartRateStats
        {
            AvgBpm = hr.Avg,
            MaxBpm = hr.Max,
            MinBpm = hr.Min,
            Zones = hr.Zones.Count > 0 ? hr.Zones.Select(z => new HeartRateZoneInfo
            {
                Name = z.Name,
                MinPercent = z.MinPercent,
                MaxPercent = z.MaxPercent,
                Duration = ToDuration(z.Duration)
            }).ToList() : null
        };
    }

    private static PowerStats? MapPower(PowerResult? pw)
    {
        if (pw == null) return null;
        return new PowerStats
        {
            AvgWatts = pw.Avg,
            MaxWatts = pw.Max,
            NormalizedPowerWatts = pw.NormalizedPower
        };
    }

    private static CadenceStats? MapCadence(CadenceResult? cad)
    {
        if (cad == null) return null;
        return new CadenceStats { AvgRpm = cad.Avg, MaxRpm = cad.Max };
    }

    private static TemperatureStats? MapTemperature(TemperatureResult? temp)
    {
        if (temp == null) return null;
        return new TemperatureStats { AvgCelsius = temp.Avg, MinCelsius = temp.Min, MaxCelsius = temp.Max };
    }

    private static EffortStatsModel MapEffort(EffortMetrics e) => new()
    {
        NaismithTime = ToDuration(e.NaismithTime),
        ToblerTime = ToDuration(e.ToblerTime),
        MunterTime = ToDuration(e.MunterTime),
        PerformanceRatioNaismith = e.PerformanceRatioNaismith,
        PerformanceRatioTobler = e.PerformanceRatioTobler,
        KilometreEffort = e.KilometreEffort,
        ItraPoints = e.ItraPoints,
        ItraCategory = e.ItraCategory,
        EquivalentFlatDistanceKm = e.EquivalentFlatDistanceKm,
        TerrainDifficulty = new TerrainDifficultyModel
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

    private static AnomalyReportModel? MapAnomalyReport(AnomalyReport? r)
    {
        if (r == null || r.IsClean) return null;
        return new AnomalyReportModel
        {
            QualityScore = r.QualityScore,
            TotalCount = r.TotalCount,
            InfoCount = r.InfoCount,
            WarningCount = r.WarningCount,
            CriticalCount = r.CriticalCount,
            DistanceImpactM = r.TotalDistanceImpactM,
            TimeImpactS = r.TotalTimeImpactS,
            CorrectionApplied = r.CorrectionApplied,
            Anomalies = r.Anomalies.Select(a => new AnomalyItem
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

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
}
