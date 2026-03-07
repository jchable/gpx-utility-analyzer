---
title: "benchmark — Compare configurations"
sidebar_label: "benchmark"
sidebar_position: 3
slug: "/cli/benchmark"
---
Runs the analysis pipeline on a single GPX file across many different configuration combinations and produces a comparison table. This is useful for understanding the impact of each parameter on a given trace.

```bash
gpx-analyzer benchmark <file.gpx> [flags]
```

## Modes

| Mode | Description | Typical run count |
|------|------------|-------------------|
| **Reduced** (default) | Varies one axis at a time, others at defaults | ~22 |
| `--full` | Full Cartesian product of all axes | ~1248 |
| `--vary axes` | Varies only the specified axes | Depends on axes |

## Configuration axes

| Axis | Values | Description |
|------|--------|-------------|
| `preset` | `hiking`, `trail`, `cycling` | Stop detection preset + GPS outlier threshold |
| `elev-algo` | `threshold`, `douglas-peucker`, `segments` | Elevation gain algorithm |
| `elev-smoothing` | `none`, `light`, `medium`, `heavy` | Elevation smoothing level |
| `track-smoothing` | `none`, `light`, `medium`, `heavy` | GPS lat/lon smoothing |
| `dem` | `true`, `false` | DEM correction on/off |
| `elev-params` | Algorithm-specific thresholds | Threshold: 1/2/3/5 m, DP epsilon: 1.5/3/5 m, Segments: min-length × max-deviation |

In **reduced mode**, each axis is varied independently while all others remain at their default values (hiking, threshold, medium elevation smoothing, no track smoothing, DEM on, threshold=2.0).

## Flags

| Flag | Description | Default |
|------|------------|---------|
| `-o`, `--output` | Export results as CSV to this file path | _(disabled)_ |
| `--full` | Run all combinations (Cartesian product) | `false` |
| `--vary` | Comma-separated axes to vary (e.g., `preset,elev-algo`) | _(all, reduced)_ |
| `-v`, `--verbose` | Print progress to stderr | `false` |
| `--sort` | Sort results by column: `distance`, `distance-3d`, `elev-gain`, `elev-loss`, `moving-time`, `avg-speed`, `max-speed`, `stops`, `filtered`, `time` | _(input order)_ |
| `--max-hr` | Maximum heart rate (bpm) for HR zone calculation | `0` _(disabled)_ |
| `--dem-dir` | Directory of SRTM `.hgt` tiles | _(disabled)_ |
| `--dem-auto-download` | Auto-download missing SRTM tiles | `true` |
| `--dem-cache` | Cache directory for downloaded tiles | _(OS cache dir)_ |
| `--dem-max-memory` | Maximum memory (MB) for loaded DEM tiles | `0` |
| `--dem-skip-validation` | Skip post-download tile validation | `false` |

## Output

The benchmark produces a table with the following metrics per configuration:

| Metric | Description |
|--------|-------------|
| Dist 2D / 3D | Total distance in km |
| D+ / D- | Elevation gain / loss in meters |
| Max Ele / Min Ele | Extreme altitudes |
| Moving / Stopped | Time durations |
| Stops | Number of detected stops |
| Avg Spd / Max Spd | Speeds in km/h |
| Filtered | Number of GPS outlier points removed |
| Time | Computation time per run |

The table is printed to stdout as a formatted text table. Use `-o` to additionally export as CSV (importable in Excel or Google Sheets).

## Examples

**Quick comparison (reduced matrix, ~22 runs):**

```bash
gpx-analyzer benchmark my-hike.gpx
```

**Export to CSV with progress:**

```bash
gpx-analyzer benchmark my-hike.gpx -o results.csv -v
```

**Full Cartesian product (~1248 runs):**

```bash
gpx-analyzer benchmark my-hike.gpx --full -o full_results.csv -v
```

**Compare only presets and elevation algorithms:**

```bash
gpx-analyzer benchmark my-hike.gpx --vary preset,elev-algo
```

**Compare only smoothing levels:**

```bash
gpx-analyzer benchmark my-hike.gpx --vary elev-smoothing,track-smoothing
```

**Sort results by elevation gain:**

```bash
gpx-analyzer benchmark my-hike.gpx --sort elev-gain
```

**Benchmark without DEM (GPS elevation only):**

```bash
gpx-analyzer benchmark my-hike.gpx --dem-auto-download=false
```
