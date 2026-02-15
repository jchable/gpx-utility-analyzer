# gpx-analyzer (.NET)

.NET port of the [Go gpx-analyzer](../cli/) CLI, producing **identical output** (JSON, text, benchmark tables). Built as a Native AOT single-file executable for direct integration into the .NET ecosystem.

All commands, flags, algorithms and output formats are the same as the Go version. See the [Go CLI documentation](../cli/docs/) for complete reference: [analyze](../cli/docs/analyze.md), [benchmark](../cli/docs/benchmark.md), [split](../cli/docs/split.md), [merge](../cli/docs/merge.md), [elevation](../cli/docs/elevation.md), [biometrics](../cli/docs/biometrics.md), [recipes](../cli/docs/recipes.md).

## Prerequisites

- .NET 9.0 SDK or later

## Build

```bash
cd cli-dotnet
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

75 unit tests covering parsing, algorithms, formatting, and output parity with the Go CLI.

## Architecture

```
cli-dotnet/
├── src/GpxAnalyzer.Cli/
│   ├── Commands/        # analyze, benchmark, split, merge (System.CommandLine)
│   ├── Gpx/             # GPX parsing, model, extensions, writer
│   ├── Stats/           # Distance, elevation, speed, stops, biometrics
│   ├── Elevation/       # Elevation smoothing, track smoothing
│   ├── Dem/             # SRTM/HGT tile management, auto-download
│   ├── Output/          # JSON (System.Text.Json + source generators) and text formatters
│   ├── Benchmark/       # Multi-configuration comparison
│   ├── Split/           # Time-based track splitting
│   ├── Merge/           # Multi-file GPX merging
│   └── Input/           # File resolution (glob support)
└── tests/GpxAnalyzer.Cli.Tests/
    └── testdata/        # GPX test files
```

Key design choices:
- **Native AOT compatible**: `System.Text.Json` source generators, no reflection
- **Zero external dependencies** beyond `System.CommandLine`
- **IEEE 754 float64 parity** with Go for identical numeric results
- **In-place mutation** of track points (same pattern as Go)
