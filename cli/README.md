# gpx-analyzer

Go command-line tool for analyzing GPX files: distance, elevation gain/loss, speed, stop detection, biometrics (heart rate, power, cadence, temperature from GPX extensions), time-based splitting and file merging. Includes elevation smoothing and automatic correction using a digital elevation model (SRTM with auto-download of tiles).

## Installation

```bash
go install github.com/jchable/gpx-utility-analyzer/cli@latest
```

Or from source:

```bash
git clone https://github.com/jchable/gpx-utility-analyzer.git
cd gpx-utility-analyzer/cli
go build -o gpx-analyzer .
```

## Quick Start

**Analyze a GPX file:**

```bash
gpx-analyzer analyze my-hike.gpx
```

**Split a multi-day track into 24h segments:**

```bash
gpx-analyzer split alps-traverse.gpx
```

**Merge multiple files:**

```bash
gpx-analyzer merge day1.gpx day2.gpx day3.gpx -o full-hike.gpx
```

**JSON output:**

```bash
gpx-analyzer analyze my-hike.gpx --format json
```

**Heavy smoothing for noisy GPS:**

```bash
gpx-analyzer analyze trace.gpx --smoothing heavy
```

**Analyze a ride with biometrics and HR zones:**

```bash
gpx-analyzer analyze ride.gpx --max-hr 185 --preset cycling
```

**Constant-slope segment algorithm (best D+ with DEM):**

```bash
gpx-analyzer analyze pct.gpx --elevation-algo segments
```

**GPS track smoothing + Douglas-Peucker:**

```bash
gpx-analyzer analyze pct.gpx --track-smoothing medium --elevation-algo douglas-peucker
```

**Export GPX with corrected elevations:**

```bash
gpx-analyzer analyze my-hike.gpx --export ./processed/
```

For complete command documentation, flags and advanced examples, see [docs/CLI_USAGE.md](docs/CLI_USAGE.md).

## Development

### Prerequisites

- Go 1.22+

### Build

```bash
go build -o gpx-analyzer .
```

### Tests

```bash
go test ./...
```
