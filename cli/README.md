# gpx-analyzer (.NET)

.NET CLI tool for analyzing GPX files: distance, elevation gain/loss, speed, stop detection, biometrics (heart rate, power, cadence, temperature from GPX extensions), time-based splitting, file merging. Includes altitude correction using a digital elevation model (SRTM). Built as a Native AOT single-file executable with no runtime dependency.

## Installation

### Windows — winget (recommended)

```powershell
winget install Coderise.gpx-analyzer
```

Open a new terminal and run `gpx-analyzer --help` — the installer adds `gpx-analyzer` to your `PATH`.

Pre-built binaries for Windows, Linux, and macOS are also available. See [docs/INSTALL.md](docs/INSTALL.md) for installation via **winget**, **apt**, or portable archives.

## Prerequisites (build from source)

- .NET 9.0 SDK or later

## Build

```bash
cd cli
dotnet build src/GpxAnalyzer.Cli/
```

## Run

```bash
dotnet run --project src/GpxAnalyzer.Cli/ -- analyze my-hike.gpx
dotnet run --project src/GpxAnalyzer.Cli/ -- analyze my-hike.gpx --format json
dotnet run --project src/GpxAnalyzer.Cli/ -- benchmark my-hike.gpx
```

## Publish (Native AOT single-file)

```bash
# Windows
dotnet publish src/GpxAnalyzer.Cli/ -c Release -r win-x64

# Linux
dotnet publish src/GpxAnalyzer.Cli/ -c Release -r linux-x64

# macOS
dotnet publish src/GpxAnalyzer.Cli/ -c Release -r osx-arm64
```

The output is a self-contained, single-file native executable (`gpx-analyzer` / `gpx-analyzer.exe`) with no .NET runtime dependency.

## Tests

```bash
dotnet test tests/GpxAnalyzer.Cli.Tests/
```

223 unit tests covering parsing, algorithms, formatting, mapping, anomaly detection, activity type detection, and integration.

## Anomaly Detection

The CLI automatically detects GPS and sensor data quality issues during analysis. Detection is always enabled with negligible performance overhead.

### Detected Anomalies (14 types in 6 categories)

| Category | Type | Severity | Description |
|----------|------|----------|-------------|
| Position | GPS Frozen | Critical | Consecutive points at identical coordinates while biometrics indicate movement |
| Position | GPS Teleportation | — | Point-to-point speed exceeds threshold; removed by GPS filter before analysis |
| Position | Signal Loss | Warning | Time gaps between consecutive points (> 30s) |
| Position | GPS Drift | Warning | Position oscillation during stops |
| Speed | Speed Spike | Warning | Points exceeding max speed threshold (already clamped) |
| Speed | Speed/Biometric Mismatch | Warning | Active cadence with zero movement |
| Elevation | Elevation Spike | Warning | Sudden elevation changes (pre-smoothing) |
| Elevation | Impossible Grade | Warning | Grade exceeding 80% |
| Temporal | Backward Time | Critical | Timestamps going backwards |
| Temporal | Duplicate Timestamp | Info | Consecutive identical timestamps |
| Biometric | HR Spike | Warning | Heart rate changes > 30 bpm between points |
| Biometric | HR Out of Range | Warning | Heart rate outside 30-230 bpm |
| Data Quality | Low Point Density | Warning | Less than 5 points per km |
| Data Quality | Constant Elevation | Warning/Critical | No elevation variation (barometer failure) |

### Quality Score

Each trace receives a quality score from 0 to 100, deducting per anomaly: Critical (-15), Warning (-5), Info (-1).

### Correction (opt-in)

Use `--fix-anomalies` to apply automatic corrections for correctable anomalies (GPS frozen interpolation, drift collapse, timestamp fixes, elevation spike interpolation, HR out-of-range exclusion). Corrections recalculate affected stats automatically.

```bash
# Default: detection only
dotnet run --project src/GpxAnalyzer.Cli/ -- analyze my-hike.gpx --preset trail

# With corrections applied
dotnet run --project src/GpxAnalyzer.Cli/ -- analyze my-hike.gpx --preset trail --fix-anomalies
```

### Output

Text format shows a "Data Quality" section with summary table and detailed anomaly list. JSON format includes a full `anomalies` object with all anomaly details.

## Architecture

```
cli/
├── src/GpxAnalyzer.Cli/           # CLI Exe (Native AOT, System.CommandLine)
│   ├── Commands/                  # analyze, benchmark, split, merge
│   └── Output/JsonContext.cs      # Source-generated JSON serialization (AOT)
├── src/GpxAnalyzer.Cli.Core/     # Shared library (referenced by CLI Exe and API)
│   ├── Gpx/                       # GPX parsing, model, extensions, writer
│   ├── Stats/                     # Distance, elevation, speed, stops, biometrics
│   ├── Elevation/                 # Elevation smoothing, track smoothing
│   ├── Dem/                       # SRTM/HGT tile management, auto-download
│   ├── Anomaly/                   # Anomaly detection (14 types), correction, quality scoring
│   ├── Output/                    # JSON/text formatters, SummaryMapper
│   ├── Benchmark/                 # Multi-configuration comparison
│   ├── Split/                     # Time-based track splitting
│   ├── Merge/                     # Multi-file GPX merging
│   └── Input/                     # File resolution (glob support)
└── tests/GpxAnalyzer.Cli.Tests/
    └── testdata/                  # GPX test files
```

Key design choices:
- **Native AOT compatible**: `System.Text.Json` source generators, no reflection
- **Zero external dependencies** beyond `System.CommandLine`
- **In-place mutation** of track points for performance
- **Core library** shared with the Web API for in-process GPX analysis
- **Anomaly detection** always on (O(n) overhead), correction opt-in via `--fix-anomalies`
