# CLI Usage — gpx-analyzer

Complete documentation of all commands, flags and usage examples.

---

## Table of contents

- [analyze — Analyze GPX files](#analyze--analyze-gpx-files)
- [split — Split a GPX by time intervals](#split--split-a-gpx-by-time-intervals)
- [merge — Merge multiple GPX files](#merge--merge-multiple-gpx-files)
- [Computed statistics](#computed-statistics)
- [Elevation correction](#elevation-correction)
- [Elevation gain algorithms](#elevation-gain-algorithms---elevation-algo)
- [GPS track smoothing](#gps-track-smoothing---track-smoothing)
- [Stop detection presets](#stop-detection-presets)
- [Common use cases](#common-use-cases)
- [Performance tuning](#performance-tuning)

---

## `analyze` — Analyze GPX files

Computes full statistics for one or more GPX files.

```bash
gpx-analyzer analyze [files...] [flags]
```

**Accepted inputs**: `.gpx` files, directories (analyzes all `.gpx` files they contain), or glob patterns (`*.gpx`).

### Flags

| Flag | Description | Default |
|------|------------|---------|
| `--format` | Output format: `text` or `json` | `text` |
| `--smoothing` | Elevation smoothing: `none`, `light`, `medium`, `heavy` | `medium` |
| `--dem-dir` | Directory of SRTM `.hgt` tiles for DEM correction | _(disabled)_ |
| `--dem-auto-download` | Automatically download missing SRTM tiles | `true` |
| `--dem-cache` | Cache directory for downloaded tiles | _(OS cache dir)_ |
| `--elevation-threshold` | Minimum elevation change threshold (meters) | `2.0` |
| `--elevation-algo` | Elevation gain algorithm: `threshold`, `douglas-peucker`, `segments` | `threshold` |
| `--track-smoothing` | GPS track lat/lon smoothing: `none`, `light`, `medium`, `heavy` | `none` |
| `--dp-epsilon` | Douglas-Peucker: max tolerated vertical deviation (meters) | `3.0` |
| `--seg-min-length` | Segments: minimum segment length (meters) | `200.0` |
| `--seg-max-deviation` | Segments: max RMS residual per segment (meters) | `2.0` |
| `--preset` | Stop detection preset: `hiking`, `trail`, `cycling` | `hiking` |
| `--stop-speed` | Override max speed for a stop (m/s) | _(per preset)_ |
| `--stop-duration` | Override min duration for a stop (e.g., `2m`) | _(per preset)_ |
| `--export` | Export reprocessed GPX files (DEM + smoothing) to this directory | _(disabled)_ |
| `--enrich` | Include computed metrics (speed, distance, grade) and biometrics as GPX extensions in export | `false` |
| `--max-hr` | Maximum heart rate (bpm) for HR zone calculation | `0` _(disabled)_ |

### Examples

**Simple analysis of a file:**

```bash
gpx-analyzer analyze my-hike.gpx
```

**Analyze all GPX files in a directory:**

```bash
gpx-analyzer analyze ./my-tracks/
```

**JSON output for integration with other tools:**

```bash
gpx-analyzer analyze my-hike.gpx --format json
```

```json
{
  "filename": "my-hike.gpx",
  "total_distance_m": 24532.5,
  "total_distance_km": 24.5,
  "elevation_gain_m": 1250.0,
  "elevation_loss_m": 1180.0,
  "avg_speed_kmh": 4.2,
  ...
}
```

**Extract a single value with `jq`:**

```bash
gpx-analyzer analyze my-hike.gpx --format json | jq '.elevation_gain_m'
```

**Disable elevation smoothing (raw GPS data):**

```bash
gpx-analyzer analyze my-hike.gpx --smoothing none
```

**Heavy smoothing for very noisy GPS data:**

```bash
gpx-analyzer analyze gps-watch-track.gpx --smoothing heavy
```

**DEM elevation correction (SRTM tiles):**

```bash
gpx-analyzer analyze pct.gpx --dem-dir ./srtm-tiles/
```

**Combine DEM + light smoothing + 3m threshold:**

```bash
gpx-analyzer analyze pct.gpx --dem-dir ./srtm-tiles/ --smoothing light --elevation-threshold 3
```

**Use the cycling preset for stop detection:**

```bash
gpx-analyzer analyze bike-ride.gpx --preset cycling
```

**Customize stop thresholds (speed < 0.2 m/s for > 5 min):**

```bash
gpx-analyzer analyze ultra-trail.gpx --stop-speed 0.2 --stop-duration 5m
```

**Analyze multiple files with glob patterns:**

```bash
gpx-analyzer analyze vacation-*.gpx --format json
```

**Export GPX with DEM-corrected elevations:**

```bash
gpx-analyzer analyze my-hike.gpx --export ./processed/
```

Produces `./processed/my-hike_processed.gpx` with DEM + smoothing elevations applied.

**Export after full reprocessing (DEM + segments):**

```bash
gpx-analyzer analyze pct.gpx --elevation-algo segments --export ./processed/
```

---

## `split` — Split a GPX by time intervals

Splits a GPX file into time-based segments. Produces one GPX file per interval and displays statistics for each segment.

```
gpx-analyzer split <file> [flags]
```

### Flags

| Flag | Description | Default |
|------|------------|---------|
| `--interval` | Split interval (e.g., `24h`, `12h`, `30m`) | `24h` |
| `--output-dir` | Output directory for GPX files | `splits` |
| `--prefix` | Prefix for generated file names | `segment` |
| `--format` | Stats output format: `text` or `json` | `text` |
| `--smoothing` | Elevation smoothing | `medium` |
| `--dem-dir` | SRTM tiles directory | _(disabled)_ |
| `--dem-auto-download` | Automatically download missing SRTM tiles | `true` |
| `--dem-cache` | Cache directory for downloaded tiles | _(OS cache dir)_ |
| `--dem-max-memory` | Maximum memory (MB) for loaded DEM tiles (0 = no limit) | `0` |
| `--dem-skip-validation` | Skip post-download DEM tile validation (faster) | `false` |
| `--elevation-threshold` | Elevation noise threshold (meters) | `2.0` |
| `--elevation-algo` | Elevation gain algorithm: `threshold`, `douglas-peucker`, `segments` | `threshold` |
| `--track-smoothing` | GPS track lat/lon smoothing | `none` |
| `--dp-epsilon` | Douglas-Peucker: max vertical deviation (meters) | `3.0` |
| `--seg-min-length` | Segments: minimum segment length (meters) | `200.0` |
| `--seg-max-deviation` | Segments: max RMS residual (meters) | `2.0` |
| `--preset` | Stop detection preset | `hiking` |
| `--stop-speed` | Override max speed for stop (m/s) | _(per preset)_ |
| `--stop-duration` | Override min duration for stop | _(per preset)_ |
| `--max-hr` | Maximum heart rate (bpm) for HR zone calculation | `0` _(disabled)_ |

### Examples

**Split a multi-day track into 24h segments:**

```bash
gpx-analyzer split alps-traverse.gpx
```

Produces:
```
splits/
  segment-001.gpx    # Day 1
  segment-002.gpx    # Day 2
  segment-003.gpx    # Day 3
  ...
```

Each segment comes with its statistics displayed in the terminal.

**Split into half-days with a custom prefix:**

```bash
gpx-analyzer split gr20.gpx --interval 12h --prefix stage --output-dir gr20-stages
```

Produces:
```
gr20-stages/
  stage-001.gpx
  stage-002.gpx
  ...
```

**Split into 30-minute intervals (useful for effort analysis):**

```bash
gpx-analyzer split marathon.gpx --interval 30m --preset trail
```

**Split with JSON stats (for automated processing):**

```bash
gpx-analyzer split tour-du-mont-blanc.gpx --format json > stages.json
```

**Split an FKT with heavy smoothing and DEM:**

```bash
gpx-analyzer split pct-karel-sabbe.gpx --interval 24h --dem-dir ./srtm/ --smoothing heavy
```

---

## `merge` — Merge multiple GPX files

Combines multiple GPX files into one. Points are sorted chronologically by default.

```
gpx-analyzer merge [files...] [flags]
```

### Flags

| Flag | Description | Default |
|------|------------|---------|
| `-o`, `--output` | Output file path | `merged.gpx` |
| `--sort` | Sort points by time | `true` |
| `--analyze` | Display statistics for the merged result | `false` |
| `--format` | Stats output format (if `--analyze`) | `text` |
| `--smoothing` | Elevation smoothing (if `--analyze`) | `medium` |
| `--dem-dir` | SRTM tiles directory (if `--analyze`) | _(disabled)_ |
| `--dem-auto-download` | Automatically download missing SRTM tiles | `true` |
| `--dem-cache` | Cache directory for downloaded tiles | _(OS cache dir)_ |
| `--dem-max-memory` | Maximum memory (MB) for loaded DEM tiles (0 = no limit) | `0` |
| `--dem-skip-validation` | Skip post-download DEM tile validation (faster) | `false` |
| `--elevation-threshold` | Elevation noise threshold (if `--analyze`) | `2.0` |
| `--elevation-algo` | Elevation gain algorithm (if `--analyze`) | `threshold` |
| `--track-smoothing` | GPS track lat/lon smoothing (if `--analyze`) | `none` |
| `--dp-epsilon` | Douglas-Peucker: max vertical deviation (if `--analyze`) | `3.0` |
| `--seg-min-length` | Segments: minimum segment length (if `--analyze`) | `200.0` |
| `--seg-max-deviation` | Segments: max RMS residual (if `--analyze`) | `2.0` |
| `--preset` | Stop detection preset (if `--analyze`) | `hiking` |
| `--max-hr` | Maximum heart rate (bpm) for HR zone calculation (if `--analyze`) | `0` _(disabled)_ |

### Examples

**Merge multiple files:**

```bash
gpx-analyzer merge day1.gpx day2.gpx day3.gpx -o full-hike.gpx
```

**Merge all GPX files in a directory and display stats:**

```bash
gpx-analyzer merge ./vacation-tracks/ -o vacation.gpx --analyze
```

**Merge segments from a previous split:**

```bash
gpx-analyzer merge ./splits/ -o reassembled.gpx --analyze
```

**Merge without sorting (keep file order):**

```bash
gpx-analyzer merge a.gpx b.gpx c.gpx -o concat.gpx --sort=false
```

**Merge with JSON analysis and DEM:**

```bash
gpx-analyzer merge ./stages/ -o full.gpx --analyze --format json --dem-dir ./srtm/
```

---

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

---

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

---

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

---

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

---

## Stop detection presets

| Preset | Max speed | Min duration | Use |
|--------|----------|-------------|-----|
| `hiking` | 0.3 m/s (1.1 km/h) | 2 min | Hiking, walking |
| `trail` | 0.5 m/s (1.8 km/h) | 1 min | Trail running, mountain running |
| `cycling` | 1.0 m/s (3.6 km/h) | 30 sec | Cycling, mountain biking |

A stop is detected when the computed speed (distance between points / elapsed time) remains below the threshold for at least the minimum duration. Thresholds can be customized with `--stop-speed` and `--stop-duration`.

---

## Biometrics

When a GPX file contains extension data (e.g., Garmin TrackPointExtension v1/v2), the CLI automatically extracts and computes biometric statistics.

### Supported extensions

| Source | Data |
|--------|------|
| Garmin TrackPointExtension v1/v2 (`<gpxtpx:hr>`, `<gpxtpx:cad>`, `<gpxtpx:atemp>`) | Heart rate, cadence, temperature |
| Standard `<power>` element | Power (watts) |

### Computed biometric metrics

| Metric | Statistics |
|--------|-----------|
| **Heart Rate** | Average, max, min (bpm). HR zones (Z1-Z5) when `--max-hr` is set |
| **Power** | Average, max (watts), normalized power (NP) |
| **Cadence** | Average, max (rpm) |
| **Temperature** | Average, min, max (°C) |

### HR zones

When `--max-hr` is provided, heart rate zones are computed based on percentage of max HR:

| Zone | Range | Description |
|------|-------|-------------|
| Z1 | 50-60% | Recovery |
| Z2 | 60-70% | Endurance |
| Z3 | 70-80% | Tempo |
| Z4 | 80-90% | Threshold |
| Z5 | 90%+ | VO2max |

### Normalized Power (NP)

Computed using a 30-second rolling average of power data, raised to the 4th power, averaged, then taking the 4th root. This metric reflects the physiological cost of variable-intensity efforts.

### Examples

**Analyze a cycling ride with HR zones:**

```bash
gpx-analyzer analyze ride.gpx --max-hr 185 --preset cycling
```

**Extract biometrics in JSON:**

```bash
gpx-analyzer analyze ride.gpx --max-hr 190 --format json | jq '.heart_rate'
```

**Split a ride and track biometrics per segment:**

```bash
gpx-analyzer split ride.gpx --interval 1h --max-hr 185 --preset cycling
```

If the GPX file does not contain extension data, biometric sections are simply omitted from the output (both text and JSON).

---

## Common use cases

### Analyze a day hike

```bash
gpx-analyzer analyze chartreuse-hike.gpx
```

### Analyze an ultra-trail with fine stop detection

```bash
gpx-analyzer analyze utmb.gpx --preset trail --stop-duration 30s
```

### Split and analyze a multi-day trek

```bash
# Split into days
gpx-analyzer split gr20-full.gpx --interval 24h --output-dir gr20-days

# View stats for each day separately
gpx-analyzer analyze ./gr20-days/

# Reassemble and verify
gpx-analyzer merge ./gr20-days/ -o gr20-verified.gpx --analyze
```

### Compare stats with and without smoothing

```bash
gpx-analyzer analyze trace.gpx --smoothing none
gpx-analyzer analyze trace.gpx --smoothing heavy
```

### Automated pipeline (JSON + jq)

```bash
# Extract distance from each file
for f in *.gpx; do
  dist=$(gpx-analyzer analyze "$f" --format json | jq '.total_distance_km')
  echo "$f: ${dist} km"
done

# Get total D+ for a directory
gpx-analyzer merge ./traces/ -o /dev/null --analyze --format json | jq '.elevation_gain_m'
```

### Get the most accurate D+ possible (DEM + segments)

```bash
gpx-analyzer analyze pct.gpx --elevation-algo segments
```

### Compare elevation gain algorithms

```bash
gpx-analyzer analyze trace.gpx --elevation-algo threshold --elevation-threshold 5
gpx-analyzer analyze trace.gpx --elevation-algo douglas-peucker --dp-epsilon 3
gpx-analyzer analyze trace.gpx --elevation-algo segments
```

### Reduce horizontal GPS noise before DEM correction

```bash
gpx-analyzer analyze trace.gpx --track-smoothing medium --elevation-algo segments
```

### Export a GPX with corrected elevations

```bash
# Export with DEM correction for use in another tool
gpx-analyzer analyze my-hike.gpx --export ./processed/

# Export with the best possible reprocessing
gpx-analyzer analyze pct.gpx --elevation-algo segments --smoothing medium --export ./clean/
```

The exported file contains coordinates and elevations after the full reprocessing pipeline (lat/lon smoothing, DEM correction, elevation smoothing). It can be imported into any GPX-compatible tool.

### Export an enriched GPX with computed metrics

```bash
gpx-analyzer analyze my-hike.gpx --export ./processed/ --enrich
```

When `--enrich` is used with `--export`, the output GPX includes per-point extensions:

- `gpxa:TrackPointMetrics` — computed speed (m/s), cumulative distance (m), grade (fraction)
- `gpxtpx:TrackPointExtension` — heart rate, cadence, power, temperature (when present in the source GPX)

This is used by the web API to precompute elevation profiles and map tracks without client-side reprocessing.

### Analyze a bike ride

```bash
gpx-analyzer analyze mountain-pass-ride.gpx --preset cycling --smoothing light
```

---

## Performance tuning

For long tracks (hundreds of km, thousands of points), processing time is dominated by DEM tile downloads and elevation computations. Here are several options to speed up analysis.

### Skip DEM correction entirely

The fastest option: rely on GPS elevation only.

```bash
gpx-analyzer analyze trace.gpx --dem-auto-download=false
```

### Skip tile validation

By default, each downloaded tile is validated (scanned for non-void data). On trusted networks or with pre-downloaded tiles, skip this step:

```bash
gpx-analyzer analyze trace.gpx --dem-skip-validation
```

### Disable smoothing

Both elevation smoothing and track smoothing add processing overhead. Disable them for raw analysis:

```bash
gpx-analyzer analyze trace.gpx --smoothing none --track-smoothing none
```

### Use the simplest elevation algorithm

The `threshold` algorithm (default) is the fastest. `douglas-peucker` and `segments` are more accurate but slower:

```bash
gpx-analyzer analyze trace.gpx --elevation-algo threshold
```

### Limit DEM memory usage

For systems with limited RAM, set a memory cap. If the required tiles exceed the limit, the analysis stops with an explicit error rather than consuming all available memory:

```bash
# Allow up to 100 MB for DEM tiles (~35 SRTM3 tiles or ~4 SRTM1 tiles)
gpx-analyzer analyze trace.gpx --dem-max-memory 100
```

Each SRTM3 tile uses ~2.8 MB in memory, and each SRTM1 tile uses ~25 MB.

### Use pre-downloaded tiles

Avoid download latency by pre-downloading tiles for your area:

```bash
# First run: tiles are downloaded and cached automatically
gpx-analyzer analyze region-track.gpx

# Subsequent runs: cached tiles are reused instantly
gpx-analyzer analyze another-track.gpx
```

Tiles are cached in a hierarchical structure (e.g. `N48/N48E002.hgt`) under the OS cache directory. Use `--dem-cache` to point to a custom location.

### Combine options for maximum speed

```bash
gpx-analyzer analyze trace.gpx \
  --smoothing none \
  --track-smoothing none \
  --dem-skip-validation \
  --elevation-algo threshold
```

### Summary of performance-related flags

| Flag | Effect on speed | Trade-off |
|------|----------------|----------|
| `--dem-auto-download=false` | ⭐⭐⭐ Fastest | No DEM correction, GPS elevation only |
| `--dem-skip-validation` | ⭐ Slight | No corrupt tile detection |
| `--smoothing none` | ⭐ Slight | More noise in elevation data |
| `--track-smoothing none` | ⭐ Slight | More horizontal GPS noise |
| `--elevation-algo threshold` | ⭐ (vs segments) | Less accurate D+/D- |
| `--dem-max-memory N` | N/A (safety) | Prevents OOM on large tracks |
