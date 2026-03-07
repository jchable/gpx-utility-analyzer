---
title: "Recipes & Performance"
sidebar_label: "Recipes & Perf"
sidebar_position: 8
slug: "/cli/recipes"
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

### Diagnose GPS issues and fix anomalies

```bash
# Check data quality (detection is always enabled)
gpx-analyzer analyze broken-gps.gpx --preset trail --format json | jq '.anomalies'

# Apply corrections and compare
gpx-analyzer analyze broken-gps.gpx --preset trail
gpx-analyzer analyze broken-gps.gpx --preset trail --fix-anomalies
```

### Benchmark a trace across all configurations

```bash
gpx-analyzer benchmark my-hike.gpx -o results.csv -v
```

### Analyze a bike ride

```bash
gpx-analyzer analyze mountain-pass-ride.gpx --preset cycling --smoothing light
```

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
| `--dem-auto-download=false` | Fastest | No DEM correction, GPS elevation only |
| `--dem-skip-validation` | Slight | No corrupt tile detection |
| `--smoothing none` | Slight | More noise in elevation data |
| `--track-smoothing none` | Slight | More horizontal GPS noise |
| `--elevation-algo threshold` | Slight (vs segments) | Less accurate D+/D- |
| `--dem-max-memory N` | N/A (safety) | Prevents OOM on large tracks |
