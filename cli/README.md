# gpx-analyzer (.NET)

.NET CLI tool for analyzing GPX files: distance, elevation gain/loss, speed, stop detection, biometrics (heart rate, power, cadence, temperature from GPX extensions), time-based splitting, file merging. Includes altitude correction using a digital elevation model (SRTM). Built as a Native AOT single-file executable with no runtime dependency.

## Prerequisites

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

79 unit tests covering parsing, algorithms, formatting, mapping, and integration.

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
