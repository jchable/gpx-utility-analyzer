# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mono-repo containing two autonomous tools for GPX file analysis:

- **cli/** (Go) — CLI for computing track statistics (distance, elevation, speed, stops) with DEM correction
- **dotnet/** (.NET) — AI-powered analysis using Microsoft Agent Framework, consuming the Go CLI's JSON output

Documentation (README, CLI_USAGE) is in French.

## Repository Structure

```text
cli/           → Go CLI project (gpx-analyzer)
dotnet/        → .NET AI analysis project (gpx-ai-analyzer)
```

## Go Project — cli/

### Go Build & Test

```bash
cd cli
go build -o gpx-analyzer .       # Build binary
go test ./...                     # Run all tests
go test -v ./internal/stats/...   # Run tests for a specific package
go test -run TestName ./internal/stats/  # Run a single test
```

Requires Go 1.25.7+.

### Go Architecture

**Entry point**: `main.go` → `cmd.Execute()` (Cobra CLI framework).

**Three subcommands** in `cmd/`: `analyze`, `split`, `merge`.

**Core processing pipeline** (in `stats.Compute()`):

1. Track smoothing (lat/lon) → `internal/elevation/tracksmooth.go`
2. DEM correction (SRTM) → `internal/dem/`
3. Speed enrichment → `internal/stats/speed.go`
4. Elevation smoothing → `internal/elevation/smooth.go`
5. Distance calculation (Haversine) → `internal/stats/distance.go`
6. Elevation gain/loss via algorithm → `internal/stats/elevation.go`
7. Stop detection → `internal/stats/stops.go`

**Key packages**: `internal/gpx/` (parsing/model/export), `internal/stats/` (computation), `internal/elevation/` (smoothing), `internal/dem/` (SRTM tiles), `internal/output/` (Formatter interface: text/JSON).

### Go Key Patterns

- Configuration objects: `ComputeConfig`, `StopConfig`, `ElevationConfig`
- In-place slice mutation: `EnrichPoints()`, `SmoothElevations()`
- Presets: stop detection (hiking/trail/cycling), smoothing (none/light/medium/heavy)

### JSON Contract

The JSON output from `analyze --format json` (defined in `internal/output/json.go`) is the contract between Go and .NET projects.

## .NET Project — dotnet/

### .NET Build & Test

```bash
dotnet build dotnet/src/GpxAiAnalyzer/GpxAiAnalyzer.csproj
dotnet test dotnet/tests/GpxAiAnalyzer.Tests/
```

Requires .NET 9.0+.

### .NET Architecture

**Entry point**: `Program.cs` → System.CommandLine CLI.

**Data flow**: JSON stdin/file → `GpxStats` deserialization → `ProviderRegistry` → `TrackAnalyzer` (Agent Framework) → `TrackReport` → `ReportFormatter`.

**Key components**:

- `Providers/` — `IChatClientProvider` interface + `ProviderRegistry` for dynamic multi-provider selection (azure-openai, openai, anthropic, ollama)
- `Analysis/` — `TrackAnalyzer` (agent orchestration), `PromptBuilder` (prompt construction), `AnalysisTools` (pure function tools for the agent)
- `Models/` — `GpxStats` (Go JSON deserialization), `TrackReport` (structured AI output)
- `Output/` — `ReportFormatter` (text/JSON)

### Adding a New AI Provider

1. Create class implementing `IChatClientProvider`
2. Register in `Program.cs`: `registry.Register(new XxxProvider())`
3. Add NuGet SDK package
