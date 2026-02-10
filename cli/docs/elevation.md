# Elevation & Statistics

## Computed statistics

| Category | Statistics |
|----------|-----------|
| **Distance** | Total 2D distance (Haversine), 3D distance (with slope) |
| **Elevation** | D+ / D- (3 algorithms available), max altitude, min altitude |
| **Time** | Total duration, moving time, stopped time, start date, end date |
| **Speed** | Average speed, average moving speed, max speed |
| **Pace** | Average pace (min/km), average moving pace |
| **Stops** | Number of stops, total duration, longest stop, average duration |
| **Biometrics** | Heart rate (avg/max/min, HR zones with `--max-hr`), power (avg/max, normalized power), cadence (avg/max), temperature (avg/min/max) |
| **Metadata** | Number of points, number of segments, point density per km |

## Elevation correction

Raw GPS elevations are often very noisy (10 to 50 meter errors are common). This artificially inflates D+ and D-. The tool offers two correction mechanisms that can be combined.

### Software smoothing (`--smoothing`)

Two-pass filter applied to elevation data before any computation:

1. **Median filter** — removes isolated spikes (an outlier point is replaced by the median value of its neighbors)
2. **Moving average** — smooths remaining high-frequency noise

| Preset | Median window | Average window | Recommended use |
|--------|--------------|----------------|-----------------|
| `none` | _(disabled)_ | _(disabled)_ | Already clean data or debugging |
| `light` | 3 points | 3 points | Good quality GPS (recent Garmin) |
| `medium` | 5 points | 5 points | General use (default) |
| `heavy` | 7 points | 11 points | Very noisy GPS (watch, phone) |

### DEM/SRTM correction

Replaces GPS elevations with those from a digital elevation model (NASA SRTM). This is the most accurate method.

#### Automatic download (default)

By default, missing SRTM tiles are **automatically downloaded** from the AWS Elevation Tiles service (SRTM1, 30m resolution when available). Tiles are cached locally:

- **Windows**: `%LOCALAPPDATA%\gpx-utility-analyzer\srtm\`
- **macOS**: `~/Library/Caches/gpx-utility-analyzer/srtm/`
- **Linux**: `~/.cache/gpx-utility-analyzer/srtm/`

```bash
# Works out of the box, tiles are downloaded on the fly
gpx-analyzer analyze my-hike.gpx
```

To disable automatic download:

```bash
gpx-analyzer analyze my-hike.gpx --dem-auto-download=false
```

To change the cache directory:

```bash
gpx-analyzer analyze my-hike.gpx --dem-cache /path/to/cache
```

#### Local tiles (`--dem-dir`)

To use SRTM1 tiles (30m, more accurate) or work offline:

1. Download SRTM tiles covering your track from [NASA Earthdata](https://earthexplorer.usgs.gov/) or [CGIAR-CSI](https://srtm.csi.cgiar.org/)
2. Place the `.hgt` files in a directory (e.g., `./srtm/`)
3. Pass `--dem-dir ./srtm/`

```bash
gpx-analyzer analyze pct.gpx --dem-dir ./srtm-tiles/
```

Files use the standard HGT format (SRTM1 at 30m or SRTM3 at 90m resolution). Naming follows the convention `N48W003.hgt` (coordinates of the tile's southwest corner).

When `--dem-dir` is provided with `--dem-auto-download` (default), local tiles take priority. If a tile is missing locally, it is downloaded to the cache. If the download fails, the GPS elevation is kept with a warning.

#### Limitations

- Automatic download requires an internet connection
- The AWS service provides SRTM1 tiles (30m) between 60°N and 56°S, and SRTM3 (90m) elsewhere

**Example: impact on a 4000+ km track (Karel Sabbe's PCT)**

| Configuration | D+ | Max altitude |
|--------------|-----|-------------|
| `--smoothing none` | 599 323 m | 7 583 m |
| `--smoothing medium` (default) | 226 908 m | 5 720 m |
| `--smoothing heavy` | 155 015 m | 5 645 m |
| DEM + `--smoothing medium` + 5m threshold | ~126 000 m | ~4 001 m |
| DEM + `--elevation-algo segments` | **~104 000 m** | ~4 001 m |

The actual D+ of the PCT is approximately 96 000 m. The `segments` algorithm combined with DEM gives the closest result.

## Elevation gain algorithms (`--elevation-algo`)

Three algorithms are available for computing D+ and D-. They are applied after elevation smoothing (`--smoothing`) and DEM correction.

### `threshold` (default)

Accumulates D+/D- only when the elevation change since the last reference point exceeds the threshold (`--elevation-threshold`). Simple and effective for filtering GPS noise.

```bash
gpx-analyzer analyze trace.gpx --elevation-algo threshold --elevation-threshold 3
```

### `douglas-peucker`

Simplifies the elevation profile (cumulative distance, altitude) using the Douglas-Peucker algorithm, then computes D+/D- on the retained points. The epsilon (`--dp-epsilon`) controls the maximum tolerated vertical deviation in meters.

```bash
gpx-analyzer analyze trace.gpx --elevation-algo douglas-peucker --dp-epsilon 3
```

Works well on GPS data without DEM. With DEM, the terrain profile retains many legitimate micro-variations, which limits the filter's effectiveness.

### `segments`

Divides the profile into quasi-constant slope segments using greedy linear regression. D+/D- is computed from the fitted elevations at segment endpoints.

```bash
gpx-analyzer analyze trace.gpx --elevation-algo segments --seg-min-length 200 --seg-max-deviation 2
```

| Parameter | Description | Default |
|-----------|------------|---------|
| `--seg-min-length` | Minimum horizontal segment length (meters) | `200.0` |
| `--seg-max-deviation` | Maximum RMS residual before splitting a segment (meters) | `2.0` |

This is the most effective algorithm with DEM data: it absorbs SRTM grid noise and produces results close to actual terrain.

## GPS track smoothing (`--track-smoothing`)

Applies a moving average to lat/lon coordinates **before** DEM correction. Reduces horizontal GPS noise that causes artificial altitude oscillations when points oscillate between different DEM cells.

| Preset | Window | Use |
|--------|--------|-----|
| `none` | _(disabled)_ | Default, no lat/lon smoothing |
| `light` | 3 points | Good quality GPS |
| `medium` | 5 points | Standard GPS |
| `heavy` | 9 points | Very noisy GPS |

```bash
gpx-analyzer analyze trace.gpx --track-smoothing medium --elevation-algo douglas-peucker
```

**Note**: lat/lon smoothing modifies the coordinates used for distance calculation and stop detection. Total distance will be slightly reduced (horizontal noise is filtered out).

### Full pipeline

The processing order is:

```
Track smoothing (lat/lon) → DEM correction → Elevation smoothing (--smoothing) → Distance calculation → Elevation gain algorithm
```

## Stop detection presets

| Preset | Max speed | Min duration | Use |
|--------|----------|-------------|-----|
| `hiking` | 0.3 m/s (1.1 km/h) | 2 min | Hiking, walking |
| `trail` | 0.5 m/s (1.8 km/h) | 1 min | Trail running, mountain running |
| `cycling` | 1.0 m/s (3.6 km/h) | 30 sec | Cycling, mountain biking |

A stop is detected when the computed speed (distance between points / elapsed time) remains below the threshold for at least the minimum duration. Thresholds can be customized with `--stop-speed` and `--stop-duration`.
