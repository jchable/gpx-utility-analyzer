namespace GpxAnalyzer.Api.Services;

using GpxAnalyzer.Cli.Core.Dem;

/// <summary>
/// Enriches route coordinates with DEM (SRTM) elevation data.
/// Reuses DemSource from CLI Core.
/// </summary>
public class RouteElevationService
{
    private readonly ILogger<RouteElevationService> _logger;
    private readonly string _demCacheDir;

    public RouteElevationService(ILogger<RouteElevationService> logger, IConfiguration config)
    {
        _logger = logger;
        _demCacheDir = config["Storage:DemDirectory"] ?? Path.Combine("data", "dem");
    }

    /// <summary>
    /// Enriches coordinates with DEM elevation. Input/output: [lon, lat, ele?] arrays.
    /// </summary>
    public async Task<double[][]> EnrichElevationAsync(double[][] coordinates, CancellationToken ct = default)
    {
        if (coordinates.Length == 0) return coordinates;

        var demSource = DemSource.CreateAuto(_demCacheDir)
            .WithMaxMemory(512);

        // Preload required SRTM tiles
        var trackPoints = coordinates.Select(c => new Cli.Core.Gpx.TrackPoint
        {
            Lat = c[1],
            Lon = c[0],
            Ele = c.Length > 2 ? c[2] : 0
        }).ToList();

        try
        {
            await demSource.PreloadAsync(trackPoints);

            // Enrich each point
            var result = new double[coordinates.Length][];
            for (int i = 0; i < coordinates.Length; i++)
            {
                var c = coordinates[i];
                var (elevation, ok) = demSource.GetElevation(c[1], c[0]);
                result[i] = ok
                    ? [c[0], c[1], elevation]
                    : [c[0], c[1], c.Length > 2 ? c[2] : 0];
            }

            _logger.LogInformation("DEM enrichment: {Points} points enriched", coordinates.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DEM enrichment failed, returning original coordinates");
            return coordinates;
        }
    }
}
