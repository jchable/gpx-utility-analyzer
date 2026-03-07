---
title: "split — Split by time intervals"
sidebar_label: "split"
sidebar_position: 4
slug: "/cli/split"
---
Splits a GPX file into time-based segments. Produces one GPX file per interval and displays statistics for each segment.

```bash
gpx-analyzer split <file> [flags]
```

## Flags

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

## Examples

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
