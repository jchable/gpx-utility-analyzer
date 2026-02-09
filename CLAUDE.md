# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mono-repo containing a full-stack GPX activity analysis platform:

- **cli/** (Go) — CLI for computing track statistics (distance, elevation, speed, stops) with DEM correction
- **ai-analyzer/** (.NET) — AI-powered analysis using Microsoft.Extensions.AI, consuming the Go CLI's JSON output
- **ui/api/** (ASP.NET Core) — Web API that orchestrates the Go CLI and AI analysis with background processing
- **ui/client/** (React) — Sport dashboard frontend (Garmin Connect style dark theme)
- **docs/** (Docusaurus) — Project documentation deployed to GitHub Pages

Documentation (README, CLI_USAGE) is in English.

## Repository Structure

```text
cli/                          → Go CLI project (gpx-analyzer)
ai-analyzer/
  src/GpxAiAnalyzer/          → .NET CLI Exe (gpx-ai-analyzer)
  src/GpxAiAnalyzer.Core/     → Shared .NET library (Models, Analysis, Providers, Output)
  tests/GpxAiAnalyzer.Tests/  → xUnit tests
ui/
  api/                        → ASP.NET Core Web API (references GpxAiAnalyzer.Core)
  client/                     → React + Vite + TailwindCSS v4 + MapLibre GL JS
docs/                         → Docusaurus documentation site
docker-compose.yml            → Dev compose (SQLite)
docker-compose.prod.yml       → Prod overlay (PostgreSQL)
.github/workflows/            → CI/CD (docs deployment to GitHub Pages)
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

Requires Go 1.25.7+. Key dependencies: `spf13/cobra` (CLI), `olekukonko/tablewriter` (text output).

### Go Architecture

**Entry point**: `main.go` → `cmd.Execute()` (Cobra CLI framework).

**Three subcommands** in `cmd/`: `analyze`, `split`, `merge`.

**Core processing pipeline** (in `stats.Compute()`):

1. GPS outlier filtering (max speed threshold) → `internal/stats/filter.go`
2. Track smoothing (lat/lon) → `internal/elevation/tracksmooth.go`
3. DEM preload (parallel download + memory check) → `internal/dem/preload.go`
4. DEM correction (SRTM, via `ElevationProvider` interface) → `internal/dem/source.go`
5. Elevation smoothing → `internal/elevation/smooth.go`
6. Speed enrichment → `internal/stats/speed.go`
7. Distance calculation (Haversine 2D + 3D) → `internal/stats/distance.go`
8. Elevation gain/loss via configurable algorithm → `internal/stats/elevation.go`
9. Stop detection → `internal/stats/stops.go`
10. Biometrics computation (HR, power, cadence, temperature) → `internal/stats/biometrics.go`

**Key packages**:
- `internal/gpx/` — parsing (`parser.go`), model (`model.go`), GPX extensions (`extensions.go`), export (`writer.go`)
- `internal/stats/` — all computation, `Summary` struct (`summary.go`), interfaces `ElevationProvider`/`ElevationPreloader`
- `internal/elevation/` — elevation smoothing (`smooth.go`), track lat/lon smoothing (`tracksmooth.go`)
- `internal/dem/` — SRTM tile management: download (`download.go`), HGT parsing (`hgt.go`), `Source` (`source.go`), preload (`preload.go`)
- `internal/output/` — `Formatter` interface: text (`text.go`) / JSON (`json.go`)
- `internal/input/` — file resolution (glob support)
- `internal/split/` — time-based track splitting
- `internal/merge/` — multi-file GPX merging

### Go Key Patterns

- Configuration objects: `ComputeConfig`, `StopConfig`, `ElevationConfig`, `BiometricsConfig`
- In-place slice mutation: `EnrichPoints()`, `SmoothElevations()`, `SmoothTrack()`
- Presets: stop detection (`hiking`/`trail`/`cycling`), smoothing (`none`/`light`/`medium`/`heavy`)
- Elevation algorithms: `threshold` (default), `douglas-peucker`, `segments`
- GPS outlier filtering: per-preset max speed thresholds or `--max-speed` override
- `Compute()` returns `(Summary, []TrackPoint, error)` — processed points enable GPX re-export via `--export`

### JSON Contract

The JSON output from `analyze --format json` (defined in `internal/output/json.go`) is the contract between Go and .NET projects. The `jsonSummary` struct defines the schema. Optional biometric fields (`heart_rate`, `power`, `cadence`, `temperature`) are included when GPX extension data is present, omitted otherwise. The `filtered_points` field appears when GPS outliers were removed.

## .NET Project — ai-analyzer/

### .NET Build & Test

```bash
# Shared Core library
dotnet build ai-analyzer/src/GpxAiAnalyzer.Core/GpxAiAnalyzer.Core.csproj

# CLI executable
dotnet build ai-analyzer/src/GpxAiAnalyzer/GpxAiAnalyzer.csproj

# Tests (xUnit)
dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/
```

Requires .NET 9.0+.

### .NET Architecture

Two projects with a shared library pattern:

**GpxAiAnalyzer.Core** (class library, namespace `GpxAiAnalyzer.Core.*`) — shared by both the CLI and the Web API:
- `Models/` — `GpxStats` (Go JSON deserialization), `TrackReport` (structured AI output)
- `Analysis/` — `TrackAnalyzer` (agent orchestration via `Microsoft.Extensions.AI`), `PromptBuilder`, `AnalysisTools` (pure function tools)
- `Providers/` — `IChatClientProvider` interface + `ProviderRegistry` for dynamic multi-provider selection
- `Output/` — `ReportFormatter` (text/JSON)

**GpxAiAnalyzer** (CLI exe) — `Program.cs` → `System.CommandLine` CLI. Data flow: JSON stdin/file → `GpxStats` → `ProviderRegistry` → `TrackAnalyzer` → `TrackReport` → `ReportFormatter`.

**Supported AI providers** (registered in `ProviderRegistry`): `azure-openai`, `openai`, `anthropic`, `mistral`, `ollama`, `gemini`.

Key packages: `Microsoft.Extensions.AI` (chat client abstraction), `Anthropic.SDK`, `Mistral.SDK`, `OllamaSharp`, `Azure.AI.OpenAI`.

## Web App — ui/

### API Build & Run

```bash
dotnet build ui/api/GpxAnalyzer.Api.csproj
dotnet run --project ui/api/GpxAnalyzer.Api.csproj    # Starts on http://localhost:5000
```

### Client Build & Run

```bash
cd ui/client
npm install
npm run dev      # Vite dev server on http://localhost:5173 (proxies /api → :5000)
npm run build    # Production build (tsc + vite)
npm run lint     # ESLint
```

### API Architecture

**Entry point**: `Program.cs` (minimal hosting, no Startup class). Namespace: `GpxAnalyzer.Api`.

**Controllers** (`Controllers/`):
- `ActivitiesController` — CRUD + upload + reanalyze + GPX download (`/api/activities`)
- `DashboardController` — aggregated summary stats (`/api/dashboard/summary`)
- `IntegrationsController` — OAuth connect/disconnect/callback (`/api/integrations`)
- `WebhooksController` — Strava webhook handler (`/api/webhooks/strava`)

**API endpoints**:
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/activities` | List activities (paginated, filterable by type) |
| GET | `/api/activities/{id}` | Activity detail with stats + AI report |
| POST | `/api/activities/upload` | Upload GPX file (multipart form, max 100 MB) |
| DELETE | `/api/activities/{id}` | Delete activity |
| POST | `/api/activities/{id}/reanalyze` | Re-trigger processing pipeline |
| GET | `/api/activities/{id}/gpx` | Download original GPX file |
| GET | `/api/dashboard/summary` | Dashboard aggregates |
| GET | `/api/integrations` | List integration status |
| POST | `/api/integrations/{provider}/connect` | Start OAuth flow |
| GET | `/api/integrations/{provider}/callback` | OAuth callback |
| DELETE | `/api/integrations/{provider}` | Disconnect integration |
| GET/POST | `/api/webhooks/{provider}` | Webhook validation/handling |

**Services** (`Services/`):
- `GpxCliService` — invokes Go CLI as subprocess (`Process.Start`), deserializes JSON to `GpxStats`
- `ActivityProcessingService` — orchestrates the 2-step pipeline: Go CLI analysis → AI analysis
- `AiAnalysisService` — creates `TrackAnalyzer` from `ProviderRegistry` using configuration
- `GpxStorageService` — file-based GPX storage (GUID-prefixed filenames)

**Integrations** (`Services/Integrations/`):
- `IActivityImporter` — interface for external providers (OAuth + webhook + activity fetch)
- `StravaService` — Strava OAuth2, webhook handling, stream→GPX reconstruction

**Background processing**:
- `Channel<Guid>` (unbounded) as in-process queue
- `ActivityProcessingWorker` (`BackgroundService`) reads from channel, delegates to `ActivityProcessingService`
- Processing states: `Pending` → `Analyzing` (Go CLI) → `AiProcessing` (AI) → `Completed` / `Failed`

**Data** (`Data/`, `Entities/`):
- EF Core with dual DB support: **SQLite** (dev) / **PostgreSQL** (prod) via `Database:Provider` config
- Entities: `Activity`, `Integration`
- `ProcessingStatus` enum: `Pending`, `Analyzing`, `AiProcessing`, `Completed`, `Failed` (stored as string)
- Auto-creates DB on startup (`EnsureCreated()`)

**Configuration** (`appsettings.json`):
- `Database:Provider` — `sqlite` or `postgresql`
- `GpxCli:BinaryPath`, `GpxCli:DefaultPreset`, `GpxCli:DefaultSmoothing`, `GpxCli:DefaultTrackSmoothing`
- `AiProvider:Name`, `AiProvider:ApiKey`, `AiProvider:Endpoint`, `AiProvider:Model`
- `Storage:GpxDirectory`
- `Integrations:Strava:ClientId`, `Integrations:Strava:ClientSecret`

### Client Architecture

React 19 + TypeScript 5.9 + Vite 7 + TailwindCSS v4 + MapLibre GL JS.

**Routing** (React Router v7, lazy-loaded pages):
- `/` — Dashboard (summary stats, recent activities, radial gauges)
- `/activities` — Activity list (paginated, filterable)
- `/activities/:id` — Activity detail (map, elevation chart, AI report)
- `/upload` — GPX upload with activity type selection
- `/integrations` — Strava connection management
- `/settings` — App settings

**Key components**:
- `components/map/TrackMap.tsx` — MapLibre GL JS with 3 views (3D terrain, 3D satellite, 2D OpenTopo)
- `components/map/MapViewSwitcher.tsx` — view toggle
- `components/activity/ElevationChart.tsx` — Recharts elevation profile
- `components/activity/AiReportPanel.tsx` — AI analysis display
- `components/widgets/StatCard.tsx`, `RadialGauge.tsx` — dashboard widgets
- `components/layout/Layout.tsx`, `Sidebar.tsx` — dark theme layout with sidebar nav

**Data layer**:
- `api/client.ts` — typed API client (fetch-based, all endpoints)
- `hooks/useActivities.ts` — TanStack React Query hooks
- `types/activity.ts` — TypeScript types mirroring API DTOs and Go JSON contract

**Activity types**: `run`, `trail`, `hike`, `cycle`, `walk`, `swim`, `other` (with associated colors and labels).

## Documentation — docs/

Docusaurus 3 site. Content synced from sub-project READMEs via `scripts/sync-docs.mjs` + `sync-manifest.json`.

```bash
cd docs
npm install
npm run start    # Dev server
npm run build    # Production build
```

Deployed to GitHub Pages (`jchable.github.io/gpx-utility-analyzer/`) via GitHub Actions on push to `main` (paths: `docs/**`, `cli/README.md`, `ai-analyzer/README.md`).

## Docker

**Dev** (SQLite): `docker compose up --build`
- `api` service — multi-stage build: Go CLI + .NET publish → ASP.NET runtime (port 5000)
- `client` service — Node build → nginx (port 8080)

**Prod** (PostgreSQL): `docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d`
- Adds `db` service (PostgreSQL 17 Alpine)
- Client on port 80

API Dockerfile is a 3-stage build: Go CLI → .NET publish → ASP.NET runtime (Go binary at `/usr/local/bin/gpx-analyzer`).

## Known Pitfalls

- **EF Core SQLite + DateTimeOffset**: SQLite provider does NOT support `DateTimeOffset` in ORDER BY or WHERE. All entities use `DateTime` (UTC), not `DateTimeOffset`.
- **EF Core SQLite + SumAsync on empty set**: Returns NULL causing crash. Use `Select(a => (double?)a.Field).SumAsync() ?? 0` or materialize first.
- **EF Core SQLite + enum string conversion**: Complex query chains with string-converted enums can fail EF Core translation. Materializing with `ToListAsync()` first is safer for dashboard-style queries.
- **Go DEM memory**: Large tracks spanning many SRTM tiles can use significant memory. Use `--dem-max-memory` flag or preload handles memory checks.

## Environment

- Go 1.25.7+, .NET 9.0, Node 22+, React 19, Vite 7, TypeScript 5.9
- For local scripts and system operations, use **PowerShell** (Python is not installed)
