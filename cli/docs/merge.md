# Merge

Combines multiple GPX files into one. Points are sorted chronologically by default.

```bash
gpx-analyzer merge [files...] [flags]
```

## Flags

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

## Examples

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
