using GpxAnalyzer.Cli.Gpx;

namespace GpxAnalyzer.Cli.Stats;

public sealed class HeartRateZone
{
    public string Name { get; init; } = "";
    public int MinPercent { get; init; }
    public int MaxPercent { get; init; }
    public TimeSpan Duration { get; set; }
}

public sealed class HeartRateResult
{
    public double Avg { get; init; }
    public int Max { get; init; }
    public int Min { get; init; }
    public List<HeartRateZone> Zones { get; init; } = [];
}

public sealed class PowerResult
{
    public double Avg { get; init; }
    public int Max { get; init; }
    public double NormalizedPower { get; init; }
}

public sealed class CadenceResult
{
    public double Avg { get; init; }
    public int Max { get; init; }
}

public sealed class TemperatureResult
{
    public double Avg { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
}

public sealed class BiometricsResult
{
    public HeartRateResult? HeartRate { get; init; }
    public PowerResult? Power { get; init; }
    public CadenceResult? Cadence { get; init; }
    public TemperatureResult? Temperature { get; init; }
}

public sealed class BiometricsConfig
{
    public int MaxHR { get; init; } // max heart rate for zone calculation (0 = skip)
}

public static class BiometricsCalculator
{
    public static BiometricsResult Compute(List<TrackPoint> points, BiometricsConfig cfg) => new()
    {
        HeartRate = ComputeHeartRate(points, cfg.MaxHR),
        Power = ComputePower(points),
        Cadence = ComputeCadence(points),
        Temperature = ComputeTemperature(points)
    };

    private static HeartRateResult? ComputeHeartRate(List<TrackPoint> points, int maxHR)
    {
        double sum = 0;
        int count = 0;
        int hrMax = 0;
        int hrMin = int.MaxValue;

        foreach (var p in points)
        {
            if (p.HeartRate == null) continue;
            int hr = p.HeartRate.Value;
            sum += hr;
            count++;
            if (hr > hrMax) hrMax = hr;
            if (hr < hrMin) hrMin = hr;
        }

        if (count == 0) return null;

        var result = new HeartRateResult
        {
            Avg = sum / count,
            Max = hrMax,
            Min = hrMin,
            Zones = maxHR > 0 ? ComputeHRZones(points, maxHR) : []
        };

        return result;
    }

    private static List<HeartRateZone> ComputeHRZones(List<TrackPoint> points, int maxHR)
    {
        var zones = new List<HeartRateZone>
        {
            new() { Name = "Z1 (Recovery)", MinPercent = 50, MaxPercent = 60 },
            new() { Name = "Z2 (Endurance)", MinPercent = 60, MaxPercent = 70 },
            new() { Name = "Z3 (Tempo)", MinPercent = 70, MaxPercent = 80 },
            new() { Name = "Z4 (Threshold)", MinPercent = 80, MaxPercent = 90 },
            new() { Name = "Z5 (VO2 Max)", MinPercent = 90, MaxPercent = 100 },
        };

        double maxF = maxHR;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].HeartRate == null) continue;
            double hr = points[i].HeartRate.Value;
            double pct = (hr / maxF) * 100;
            var dt = points[i].Time - points[i - 1].Time;
            if (dt <= TimeSpan.Zero) continue;

            for (int z = 0; z < zones.Count; z++)
            {
                double lo = zones[z].MinPercent;
                double hi = zones[z].MaxPercent;
                if (z == zones.Count - 1)
                {
                    // Z5 is open-ended: 90%+
                    if (pct >= lo)
                        zones[z].Duration += dt;
                }
                else
                {
                    if (pct >= lo && pct < hi)
                        zones[z].Duration += dt;
                }
            }
        }

        return zones;
    }

    private static PowerResult? ComputePower(List<TrackPoint> points)
    {
        double sum = 0;
        int count = 0;
        int pMax = 0;

        foreach (var p in points)
        {
            if (p.Power == null) continue;
            int pw = p.Power.Value;
            sum += pw;
            count++;
            if (pw > pMax) pMax = pw;
        }

        if (count == 0) return null;

        return new PowerResult
        {
            Avg = sum / count,
            Max = pMax,
            NormalizedPower = ComputeNormalizedPower(points)
        };
    }

    private static double ComputeNormalizedPower(List<TrackPoint> points)
    {
        var samples = new List<(DateTime T, double W)>();
        foreach (var p in points)
        {
            if (p.Power == null || p.Time == DateTime.MinValue) continue;
            samples.Add((p.Time, p.Power.Value));
        }
        if (samples.Count < 2) return 0;

        var window = TimeSpan.FromSeconds(30);
        double fourthPowerSum = 0;
        int fourthPowerCount = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            double windowSum = 0;
            int windowCount = 0;
            for (int j = i; j >= 0; j--)
            {
                if (samples[i].T - samples[j].T > window)
                    break;
                windowSum += samples[j].W;
                windowCount++;
            }
            if (windowCount == 0) continue;
            double avg = windowSum / windowCount;
            fourthPowerSum += Math.Pow(avg, 4);
            fourthPowerCount++;
        }

        if (fourthPowerCount == 0) return 0;
        return Math.Pow(fourthPowerSum / fourthPowerCount, 0.25);
    }

    private static CadenceResult? ComputeCadence(List<TrackPoint> points)
    {
        double sum = 0;
        int count = 0, cMax = 0;

        foreach (var p in points)
        {
            if (p.Cadence == null) continue;
            int c = p.Cadence.Value;
            sum += c;
            count++;
            if (c > cMax) cMax = c;
        }

        if (count == 0) return null;
        return new CadenceResult { Avg = sum / count, Max = cMax };
    }

    private static TemperatureResult? ComputeTemperature(List<TrackPoint> points)
    {
        double sum = 0;
        double tMax = double.MinValue;
        double tMin = double.MaxValue;
        int count = 0;

        foreach (var p in points)
        {
            if (p.Temperature == null) continue;
            double temp = p.Temperature.Value;
            sum += temp;
            count++;
            if (temp > tMax) tMax = temp;
            if (temp < tMin) tMin = temp;
        }

        if (count == 0) return null;
        return new TemperatureResult { Avg = sum / count, Min = tMin, Max = tMax };
    }
}
