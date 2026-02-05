# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Go CLI tool for analyzing, processing, and transforming GPX files. Computes track statistics (distance, elevation, speed, stops) with DEM elevation correction, multiple elevation algorithms, and track splitting/merging. Documentation (README, CLI_USAGE) is written in French.

## Build & Test Commands

```bash
go build -o gpx-analyzer .       # Build binary
go test ./...                     # Run all tests
go test -v ./internal/stats/...   # Run tests for a specific package (verbose)
go test -run TestName ./internal/stats/  # Run a single test
```

Requires Go 1.25.7+. No Makefile or task runner — use standard `go` commands.

## Architecture

**Entry point**: `main.go` → `cmd.Execute()` (Cobra CLI framework).

**Three subcommands** defined in `cmd/`:
- `analyze` — compute statistics on GPX files
- `split` — segment tracks by time intervals
- `merge` — combine multiple GPX files

**Core processing pipeline** (in `stats.Compute()`):
1. Track smoothing (lat/lon moving average) → `internal/elevation/tracksmooth.go`
2. DEM correction (replace GPS elevations with SRTM values) → `internal/dem/`
3. Speed enrichment (per-point speed & distance) → `internal/stats/speed.go`
4. Elevation smoothing (median + moving average) → `internal/elevation/smooth.go`
5. Distance calculation (Haversine 2D + 3D) → `internal/stats/distance.go`
6. Elevation gain/loss via selected algorithm → `internal/stats/elevation.go`
7. Stop detection → `internal/stats/stops.go`

**Key packages in `internal/`:**
- `gpx/` — GPX XML parsing (`ParseFile`, `Parse`), data model (`GPX`, `TrackPoint`), and export (`WriteFile`, `NewGPXFromPoints`)
- `stats/` — statistics computation, elevation algorithms (threshold, douglas-peucker, segments), stop detection
- `elevation/` — smoothing filters with presets (none/light/medium/heavy)
- `dem/` — SRTM HGT tile loading, bilinear interpolation, auto-download from AWS, caching
- `split/` — time-based track segmentation with boundary point duplication
- `merge/` — multi-file merge with optional chronological sorting
- `input/` — file/directory/glob resolution
- `output/` — `Formatter` interface with text (table) and JSON implementations

## Key Patterns

- **Configuration objects**: `ComputeConfig`, `StopConfig`, `ElevationConfig` pass parameters through the pipeline
- **In-place slice mutation**: `EnrichPoints()`, `SmoothElevations()`, `SmoothTrack()` modify `[]TrackPoint` slices directly
- **Shared flags**: Analysis flags (smoothing, DEM, algorithm, preset) are reused across `analyze`, `split`, and `merge` commands
- **Presets**: Stop detection (hiking/trail/cycling) and smoothing (none/light/medium/heavy) use preset-based configuration

## Extension Points

- **New elevation algorithm**: add `internal/stats/elevation_*.go`, register in `ComputeElevationWithAlgo()` dispatch
- **New output format**: implement the `output.Formatter` interface
- **New CLI command**: follow existing pattern in `cmd/` (register in `root.go` init)
