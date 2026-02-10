namespace GpxAnalyzer.Api.Services;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

public class ProfileComputationService
{
    private const int ProfileTargetPoints = 500;
    private const int DefaultSmoothingWindow = 15;

    private static readonly XNamespace GpxaNs = "http://gpx-analyzer.io/extensions/v1";
    private static readonly XNamespace GpxtpxNs = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
    private static readonly XNamespace GpxtpxNsV2 = "http://www.garmin.com/xmlschemas/TrackPointExtension/v2";

    // Minetti (2002) metabolic cost at zero grade = 3.6 J/kg/m
    private static readonly double CFlat = MinettiCost(0);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILogger<ProfileComputationService> _logger;

    public ProfileComputationService(ILogger<ProfileComputationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse an enriched GPX file and compute profile + track GeoJSON.
    /// Returns (profileJson, trackGeoJson) or (null, null) if parsing fails.
    /// </summary>
    public (string? ProfileJson, string? TrackGeoJson) ComputeFromEnrichedGpx(string gpxFilePath)
    {
        try
        {
            var doc = XDocument.Load(gpxFilePath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var rawPoints = new List<RawEnrichedPoint>();

            foreach (var trkpt in doc.Descendants(ns + "trkpt"))
            {
                var lat = ParseDouble(trkpt.Attribute("lat")?.Value);
                var lon = ParseDouble(trkpt.Attribute("lon")?.Value);
                var ele = ParseDouble(trkpt.Element(ns + "ele")?.Value);
                var timeStr = trkpt.Element(ns + "time")?.Value;
                DateTime? time = timeStr != null ? DateTime.Parse(timeStr, null, DateTimeStyles.RoundtripKind) : null;

                // Parse enriched extensions (gpxa namespace)
                double speed = 0, cumDist = 0, grade = 0;
                int? hr = null, cad = null, power = null;

                var extensions = trkpt.Element(ns + "extensions");
                if (extensions != null)
                {
                    var metrics = extensions.Element(GpxaNs + "TrackPointMetrics");
                    if (metrics != null)
                    {
                        speed = ParseDouble(metrics.Element(GpxaNs + "speed")?.Value);
                        cumDist = ParseDouble(metrics.Element(GpxaNs + "dist")?.Value);
                        grade = ParseDouble(metrics.Element(GpxaNs + "grade")?.Value);
                    }

                    // Biometrics: try v1 then v2
                    var tpe = extensions.Element(GpxtpxNs + "TrackPointExtension")
                           ?? extensions.Element(GpxtpxNsV2 + "TrackPointExtension");
                    if (tpe != null)
                    {
                        hr = ParseInt(tpe.Element(tpe.Name.Namespace + "hr")?.Value);
                        cad = ParseInt(tpe.Element(tpe.Name.Namespace + "cad")?.Value);
                    }

                    power = ParseInt(extensions.Element("power")?.Value);
                }

                rawPoints.Add(new RawEnrichedPoint
                {
                    Lat = lat, Lon = lon, Ele = ele, Time = time,
                    Speed = speed, CumDist = cumDist, Grade = grade,
                    HeartRate = hr, Cadence = cad, Power = power,
                });
            }

            if (rawPoints.Count < 2)
            {
                _logger.LogWarning("Enriched GPX has {Count} points, skipping profile", rawPoints.Count);
                return (null, null);
            }

            _logger.LogInformation("Parsed {Count} enriched points from GPX", rawPoints.Count);

            var profileJson = ComputeProfile(rawPoints);
            var trackGeoJson = BuildTrackGeoJson(rawPoints);

            return (profileJson, trackGeoJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute profile from enriched GPX: {Path}", gpxFilePath);
            return (null, null);
        }
    }

    private string ComputeProfile(List<RawEnrichedPoint> points)
    {
        var n = points.Count;

        // Extract raw arrays
        var speeds = points.Select(p => p.Speed * 3.6).ToArray(); // m/s → km/h
        var grades = points.Select(p => p.Grade).ToArray();
        var elevations = points.Select(p => p.Ele).ToArray();

        // Adaptive smoothing window
        var effectiveWindow = Math.Min(DefaultSmoothingWindow, n / 3);
        var eleWindow = Math.Max(3, effectiveWindow / 3);

        // Smooth
        var smoothSpeed = RollingAverage(speeds, effectiveWindow);
        var smoothGrade = RollingAverage(grades, effectiveWindow);
        var smoothEle = RollingAverage(elevations, eleWindow);

        // Compute GAP then smooth
        var rawGap = new double[n];
        for (var i = 0; i < n; i++)
        {
            var cost = MinettiCost(smoothGrade[i]);
            rawGap[i] = smoothSpeed[i] * (cost / CFlat);
        }
        var smoothGap = RollingAverage(rawGap, effectiveWindow);

        // Elapsed time
        var startTime = points[0].Time;

        // Build full profile
        var fullProfile = new ProfilePoint[n];
        for (var i = 0; i < n; i++)
        {
            double? elapsed = null;
            if (startTime.HasValue && points[i].Time.HasValue)
                elapsed = (points[i].Time!.Value - startTime.Value).TotalSeconds;

            fullProfile[i] = new ProfilePoint
            {
                Distance = Math.Round(points[i].CumDist / 1000.0, 3),
                Elevation = Math.Round(smoothEle[i]),
                Speed = Math.Round(smoothSpeed[i], 1),
                Gap = Math.Round(smoothGap[i], 1),
                Grade = Math.Round(smoothGrade[i] * 100, 1), // fraction → percentage
                ElapsedTime = elapsed,
                HeartRate = points[i].HeartRate,
                Cadence = points[i].Cadence,
                Power = points[i].Power,
            };
        }

        // Downsample for chart
        var downsampled = Downsample(fullProfile, ProfileTargetPoints);
        return JsonSerializer.Serialize(downsampled, JsonOptions);
    }

    private static string BuildTrackGeoJson(List<RawEnrichedPoint> points)
    {
        // GeoJSON LineString with ALL points (full precision for map)
        var coordinates = points.Select(p => new[] {
            Math.Round(p.Lon, 7),
            Math.Round(p.Lat, 7),
        }).ToArray();

        var geoJson = new
        {
            type = "LineString",
            coordinates,
        };

        return JsonSerializer.Serialize(geoJson);
    }

    // ── Minetti (2002) metabolic cost model ──

    /// <summary>
    /// Metabolic cost C(i) in J/kg/m, where i is grade as a fraction.
    /// Minetti et al. (2002) polynomial.
    /// </summary>
    private static double MinettiCost(double i)
    {
        var g = Math.Clamp(i, -0.45, 0.45);
        var cost = 155.4 * Math.Pow(g, 5)
                 - 30.4 * Math.Pow(g, 4)
                 - 43.3 * Math.Pow(g, 3)
                 + 46.3 * Math.Pow(g, 2)
                 + 19.5 * g
                 + 3.6;
        return Math.Max(cost, 0.1);
    }

    // ── Smoothing & downsampling ──

    private static double[] RollingAverage(double[] values, int windowSize)
    {
        if (windowSize <= 1) return (double[])values.Clone();

        var half = windowSize / 2;
        var result = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var lo = Math.Max(0, i - half);
            var hi = Math.Min(values.Length - 1, i + half);
            var sum = 0.0;
            for (var j = lo; j <= hi; j++) sum += values[j];
            result[i] = sum / (hi - lo + 1);
        }

        return result;
    }

    private static T[] Downsample<T>(T[] data, int targetCount)
    {
        if (data.Length <= targetCount) return data;

        var result = new List<T>(targetCount) { data[0] };
        var step = (double)(data.Length - 1) / (targetCount - 1);

        for (var i = 1; i < targetCount - 1; i++)
            result.Add(data[(int)Math.Round(i * step)]);

        result.Add(data[^1]);
        return result.ToArray();
    }

    // ── Helpers ──

    private static double ParseDouble(string? s) =>
        double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static int? ParseInt(string? s) =>
        int.TryParse(s, out var v) ? v : null;

    // ── Models ──

    private class RawEnrichedPoint
    {
        public double Lat { get; init; }
        public double Lon { get; init; }
        public double Ele { get; init; }
        public DateTime? Time { get; init; }
        public double Speed { get; init; }
        public double CumDist { get; init; }
        public double Grade { get; init; }
        public int? HeartRate { get; init; }
        public int? Cadence { get; init; }
        public int? Power { get; init; }
    }
}

public class ProfilePoint
{
    public double Distance { get; set; }
    public double Elevation { get; set; }
    public double Speed { get; set; }
    public double Gap { get; set; }
    public double Grade { get; set; }
    public double? ElapsedTime { get; set; }
    public int? HeartRate { get; set; }
    public int? Cadence { get; set; }
    public int? Power { get; set; }
}
