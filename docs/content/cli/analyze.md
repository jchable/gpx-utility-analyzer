---
title: "analyze — Analyze GPX files"
sidebar_label: "analyze"
sidebar_position: 2
slug: "/cli/analyze"
---
Computes full statistics for one or more GPX files.

```bash
gpx-analyzer analyze [files...] [flags]
```

**Accepted inputs**: `.gpx` files, directories (analyzes all `.gpx` files they contain), or glob patterns (`*.gpx`).

## Flags

| Flag | Description | Default |
|------|------------|---------|
| `--format` | Output format: `text` or `json` | `text` |
| `--smoothing` | Elevation smoothing: `none`, `light`, `medium`, `heavy` | `medium` |
| `--dem-dir` | Directory of SRTM `.hgt` tiles for DEM correction | _(disabled)_ |
| `--dem-auto-download` | Automatically download missing SRTM tiles | `true` |
| `--dem-cache` | Cache directory for downloaded tiles | _(OS cache dir)_ |
| `--dem-max-memory` | Maximum memory (MB) for loaded DEM tiles (0 = no limit) | `0` |
| `--dem-skip-validation` | Skip post-download DEM tile validation (faster) | `false` |
| `--elevation-threshold` | Minimum elevation change threshold (meters) | `2.0` |
| `--elevation-algo` | Elevation gain algorithm: `threshold`, `douglas-peucker`, `segments` | `threshold` |
| `--track-smoothing` | GPS track lat/lon smoothing: `none`, `light`, `medium`, `heavy` | `none` |
| `--dp-epsilon` | Douglas-Peucker: max tolerated vertical deviation (meters) | `3.0` |
| `--seg-min-length` | Segments: minimum segment length (meters) | `200.0` |
| `--seg-max-deviation` | Segments: max RMS residual per segment (meters) | `2.0` |
| `--preset` | Stop detection preset: `hiking`, `trail`, `cycling`, `running`, `walking`, `swimming` | `hiking` |
| `--stop-speed` | Override max speed for a stop (m/s) | _(per preset)_ |
| `--stop-duration` | Override min duration for a stop (e.g., `2m`) | _(per preset)_ |
| `--export` | Export reprocessed GPX files (DEM + smoothing) to this directory | _(disabled)_ |
| `--enrich` | Include computed metrics (speed, distance, grade) and biometrics as GPX extensions in export | `false` |
| `--max-hr` | Maximum heart rate (bpm) for HR zone calculation | `0` _(disabled)_ |
| `--fix-anomalies` | Apply automatic anomaly corrections (interpolation, drift collapse, etc.) | `false` |

## Examples

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

**Analyze with anomaly corrections applied:**

```bash
gpx-analyzer analyze my-hike.gpx --preset trail --fix-anomalies
```

Anomaly detection is always active. Use `--fix-anomalies` to apply automatic corrections (GPS frozen interpolation, drift collapse, timestamp fixes, HR exclusion). See [Anomaly Detection](/gpx-utility-analyzer/docs/cli/anomalies) for details.
