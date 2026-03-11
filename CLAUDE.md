# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mono-repo containing a full-stack GPX activity analysis platform:

- **cli/** (.NET) — CLI for computing track statistics (distance, elevation, speed, stops) with DEM correction
- **ai-analyzer/** (.NET) — AI-powered analysis using Microsoft.Extensions.AI, consuming the CLI's JSON output
- **ui/api/** (ASP.NET Core) — Web API that orchestrates GPX analysis and AI reports with background processing
- **ui/client/** (React) — Sport dashboard frontend (Garmin Connect style dark theme)
- **docs/** (Docusaurus) — Project documentation deployed to GitHub Pages

Documentation (README, CLI_USAGE) is in English.

## Repository Structure

```text
cli/                          → .NET CLI project (gpx-analyzer)
  src/GpxAnalyzer.Cli/          → CLI Exe (Native AOT)
  src/GpxAnalyzer.Cli.Core/     → Shared library (Stats, Gpx, Output)
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

## .NET CLI Project — cli/

### .NET CLI Build & Test

```bash
cd cli
dotnet build src/GpxAnalyzer.Cli/            # Build CLI
dotnet build src/GpxAnalyzer.Cli.Core/       # Build shared library
dotnet test tests/GpxAnalyzer.Cli.Tests/     # Run all tests (79 tests)
```

Requires .NET 9.0+. Key dependency: `System.CommandLine` (CLI framework).

### .NET CLI Architecture

Two projects with a shared library pattern:

**GpxAnalyzer.Cli.Core** (class library, namespace `GpxAnalyzer.Cli.Core.*`) — shared by CLI Exe and Web API:
- `Gpx/` — parsing (`GpxParser.cs`), model (`TrackPoint.cs`, `GpxDocument.cs`), extensions (`GpxExtensions.cs`), export (`GpxWriter.cs`)
- `Stats/` — computation pipeline (`ComputePipeline.cs`), `Summary` struct, `ComputeConfig`, distance/elevation/speed/stops/biometrics calculators
- `Elevation/` — elevation smoothing (`ElevationSmoother.cs`), track lat/lon smoothing (`TrackSmoother.cs`)
- `Dem/` — SRTM tile management: `DemSource`, `HgtTile`, `TileDownloader`
- `Output/` — `IFormatter` interface, `JsonFormatter`/`TextFormatter`, `JsonModels` (JSON contract), `SummaryMapper` (Summary → GpxStats)
- `Split/` — time-based track splitting
- `Merge/` — multi-file GPX merging
- `Input/` — file resolution (glob support)
- `Benchmark/` — multi-configuration comparison

**GpxAnalyzer.Cli** (CLI exe, Native AOT) — `Program.cs` → `System.CommandLine`. Commands: `analyze`, `benchmark`, `split`, `merge`.
- `Commands/` — command definitions and `SharedFlags` for shared option handling
- `Output/JsonContext.cs` — source-generated JSON serialization (AOT-specific)

### Core Processing Pipeline (in `ComputePipeline.Compute()`)

1. GPS outlier filtering (max speed threshold) → `GpsFilter.cs`
2. Track smoothing (lat/lon) → `TrackSmoother.cs`
3. DEM preload (parallel download + memory check) → `DemSource.cs`
4. DEM correction (SRTM) → `DemSource.cs`
5. Elevation smoothing → `ElevationSmoother.cs`
6. Speed enrichment → `SpeedCalculator.cs`
7. Distance calculation (Haversine 2D + 3D) → `DistanceCalculator.cs`
8. Elevation gain/loss via configurable algorithm → `ElevationCalculator.cs`
9. Stop detection → `StopDetector.cs`
10. Biometrics computation (HR, power, cadence, temperature) → `BiometricsCalculator.cs`

### Key Patterns

- Configuration objects: `ComputeConfig`, `StopConfig`, `ElevationConfig`, `BiometricsConfig`
- In-place list mutation: speed/distance enrichment, smoothing
- Presets: stop detection (`hiking`/`trail`/`cycling`), smoothing (`none`/`light`/`medium`/`heavy`)
- Elevation algorithms: `threshold` (default), `douglas-peucker`, `segments`
- GPS outlier filtering: per-preset max speed thresholds or `--max-speed` override
- `Compute()` returns `(Summary, List<TrackPoint>)` — processed points enable GPX re-export via `--export`
- `--enrich` flag: writes per-point computed metrics as `gpxa:TrackPointMetrics` extensions in exported GPX
- `SummaryMapper.ToGpxStats()`: maps `Summary` to `GpxStats` for API consumption

### JSON Contract

The JSON output from `analyze --format json` (defined in `Output/JsonModels.cs`) is the contract between CLI and AI analyzer projects. The `JsonSummary` class defines the schema. Optional biometric fields (`heart_rate`, `power`, `cadence`, `temperature`) are included when GPX extension data is present, omitted otherwise. The `filtered_points` field appears when GPS outliers were removed.

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
- `Models/` — `GpxStats` (CLI JSON contract), `TrackReport` (structured AI output)
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
| GET | `/api/activities/{id}/profile` | Precomputed elevation profile (500 points, JSON, cache 1h) |
| GET | `/api/activities/{id}/track` | Full track GeoJSON LineString (all points, cache 1h) |
| GET | `/api/dashboard/summary` | Dashboard aggregates |
| GET | `/api/integrations` | List integration status |
| POST | `/api/integrations/{provider}/connect` | Start OAuth flow |
| GET | `/api/integrations/{provider}/callback` | OAuth callback |
| DELETE | `/api/integrations/{provider}` | Disconnect integration |
| GET/POST | `/api/webhooks/{provider}` | Webhook validation/handling |

**Services** (`Services/`):
- `GpxAnalysisService` — calls `ComputePipeline` in-process (from `GpxAnalyzer.Cli.Core`), maps `Summary` → `GpxStats` via `SummaryMapper`
- `ActivityProcessingService` — orchestrates the 3-step pipeline: GPX analysis → profile computation → AI analysis
- `ProfileComputationService` — parses enriched GPX extensions, computes Minetti GAP, smoothing, downsampling (500 pts for charts), full-precision GeoJSON track for map
- `AiAnalysisService` — creates `TrackAnalyzer` from `ProviderRegistry` using configuration
- `GpxStorageService` — file-based GPX storage (GUID-prefixed filenames, original archived as zip)

**Integrations** (`Services/Integrations/`):
- `IActivityImporter` — interface for external providers (OAuth + webhook + activity fetch)
- `StravaService` — Strava OAuth2, webhook handling, stream→GPX reconstruction

**Background processing**:
- `Channel<Guid>` (unbounded) as in-process queue
- `ActivityProcessingWorker` (`BackgroundService`) reads from channel, delegates to `ActivityProcessingService`
- Processing states: `Pending` → `Analyzing` (GPX analysis + profile computation) → `AiProcessing` (AI) → `Completed` / `Failed`

**Data** (`Data/`, `Entities/`):
- EF Core with dual DB support: **SQLite** (dev) / **PostgreSQL** (prod) via `Database:Provider` config
- Entities: `Activity` (includes `ProfileJson`, `TrackGeoJson` for precomputed chart/map data), `Integration`
- `ProcessingStatus` enum: `Pending`, `Analyzing`, `AiProcessing`, `Completed`, `Failed` (stored as string)
- Auto-creates DB on startup (`EnsureCreated()`)

**Configuration** (`appsettings.json`):
- `Database:Provider` — `sqlite` or `postgresql`
- `GpxCli:DefaultPreset`, `GpxCli:DefaultSmoothing`, `GpxCli:DefaultTrackSmoothing`
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
- `components/map/TrackMap.tsx` — MapLibre GL JS with 3 views (3D terrain, 3D satellite, 2D OpenTopo). Receives precomputed GeoJSON track coordinates from API
- `components/map/MapViewSwitcher.tsx` — view toggle
- `components/activity/ElevationProfileChart.tsx` — Recharts elevation/speed/GAP profile. Receives precomputed `ProfilePoint[]` from API (no client-side computation)
- `components/activity/AiReportPanel.tsx` — AI analysis display
- `components/widgets/StatCard.tsx`, `RadialGauge.tsx` — dashboard widgets
- `components/layout/Layout.tsx`, `Sidebar.tsx` — dark theme layout with sidebar nav (mobile bottom nav)
- `components/layout/OfflineBanner.tsx` — offline status indicator (shown when navigator.onLine is false)

**Data layer**:
- `api/client.ts` — typed API client (fetch-based, all endpoints, sends `Accept-Language` header). Includes `getProfile()` and `getTrack()` for precomputed data
- `hooks/useActivities.ts` — TanStack React Query hooks including `useProfile(id)` and `useTrack(id)` with 1h staleTime (immutable data)
- `hooks/useOnlineStatus.ts` — online/offline detection hook (navigator events)
- `types/activity.ts` — TypeScript types mirroring API DTOs, CLI JSON contract, and `ProfilePoint` for chart data
- `i18n.ts` — i18next initialization (react-i18next, HTTP backend, browser language detection)

**Activity types**: `run`, `trail`, `hike`, `cycle`, `walk`, `swim`, `other` (with associated colors in `ACTIVITY_COLORS`). Labels are i18n-driven via `t('activityType.xxx')`.

### PWA (Progressive Web App)

The client is a **PWA** powered by `vite-plugin-pwa` (Workbox under the hood).

**Configuration**: `vite.config.ts` — `VitePWA` plugin with `registerType: 'autoUpdate'`, manifest, and Workbox runtime caching rules.

**Service Worker registration**: `main.tsx` — `registerSW({ immediate: true })` from `virtual:pwa-register`.

**Caching strategies** (Workbox runtime caching):
- `StaleWhileRevalidate` — API list/detail endpoints (`/api/activities`, `/api/dashboard/*`, `/api/activities/{id}`) — 100 entries, 24h
- `CacheFirst` — Track/profile data (`/api/activities/{id}/track`, `/api/activities/{id}/profile`) — 50 entries, 7 days
- `CacheFirst` — GPX downloads (`/api/activities/{id}/gpx`) — 50 entries, 7 days
- Static assets (JS/CSS/fonts/images) — precached at build time

**Manifest**: `manifest.webmanifest` (auto-generated), theme/background `#0f0f1a`, display `standalone`.

**Icons** (in `public/`): `favicon.svg`, `pwa-192x192.png`, `pwa-512x512.png`, `apple-touch-icon-180x180.png` (placeholder).

**Offline support**: `useOnlineStatus` hook + `OfflineBanner` component in Layout. Cached API data is served when offline.

**nginx** (`nginx.conf`): Service worker (`sw.js`) and Workbox chunks served with `no-cache` headers. Manifest served with 1h cache.

**i18n components**:
- `components/layout/LanguageSwitcher.tsx` — EN/FR toggle in sidebar
- `i18n.ts` — i18next initialization (HTTP backend, browser language detection)

## Internationalization (i18n)

The application supports **English** (default) and **French**.

### Frontend (react-i18next)

- **Framework**: `react-i18next` with `i18next-http-backend` (loads JSON) + `i18next-browser-languagedetector`
- **Translation files**: `ui/client/public/locales/{lang}/{namespace}.json`
- **Namespaces**: `common`, `dashboard`, `activities`, `upload`, `integrations`, `settings`
- **Default namespace**: `common` (shared strings: nav, buttons, units, statuses, activity types, error codes)
- **Key convention**: flat dot-notation within namespace, e.g. `t('activityType.trail')` from common
- **Language detection**: `localStorage` → browser navigator → fallback `en`
- **LanguageSwitcher**: toggle in sidebar footer, persists via localStorage (`i18nextLng`)

**Usage pattern in components**:
```tsx
const { t } = useTranslation('activities');      // page-specific namespace
const { t: tc } = useTranslation();              // common namespace
// Use: t('title'), tc('activityType.run'), tc('button.save')
```

**Dates**: use `i18n.language` for locale-aware formatting: `new Date(iso).toLocaleDateString(i18n.language, options)`

**Pluralization**: i18next `_one`/`_other` suffixes (e.g. `fileCount_one`, `fileCount_other`)

### API Error Strategy

The API returns **structured error codes** (not localized messages):
```json
{ "code": "NO_FILE_PROVIDED" }
```
The frontend translates these via `common:apiError.{CODE}` keys. Error codes: `NO_FILE_PROVIDED`, `INVALID_FILE_TYPE`, `GPX_NOT_FOUND`, `UNKNOWN_PROVIDER`, `MISSING_OAUTH_PARAMS`, `AI_PROVIDER_NOT_CONFIGURED`.

### API Accept-Language Header

`client.ts` sends `Accept-Language` header on every request (from `i18n.language`). The API uses this to:
1. Store `Language` on the `Activity` entity at upload/reanalyze time
2. Pass the language through to AI analysis for localized reports

### AI Report Localization

Language propagation chain:
```
Frontend (Accept-Language) → ActivitiesController → Activity.Language (stored)
→ ActivityProcessingService → AiAnalysisService → TrackAnalyzer → PromptBuilder
```
`PromptBuilder.BuildAnalysisPrompt(stats, language)` appends a language instruction to the prompt when `language != "en"`. The system prompt stays in English (best LLM comprehension); only the response language instruction is localized.

### Adding a New Language

1. Create `ui/client/public/locales/{lang}/` with all 6 namespace JSON files
2. Add the language code to `supportedLngs` in `ui/client/src/i18n.ts`
3. Add language name mapping in `LanguageSwitcher.tsx`
4. Add language name in `PromptBuilder.cs` switch expression
5. Add `language.{code}` entry in all existing `common.json` files

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
- `api` service — multi-stage build: .NET publish → ASP.NET runtime (port 5000)
- `client` service — Node build → nginx (port 8080)

**Prod** (PostgreSQL): `docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d`
- Adds `db` service (PostgreSQL 17 Alpine)
- Client on port 80

API Dockerfile is a 2-stage build: .NET SDK publish → ASP.NET runtime.

## Known Pitfalls

- **EF Core SQLite + DateTimeOffset**: SQLite provider does NOT support `DateTimeOffset` in ORDER BY or WHERE. All entities use `DateTime` (UTC), not `DateTimeOffset`.
- **EF Core SQLite + SumAsync on empty set**: Returns NULL causing crash. Use `Select(a => (double?)a.Field).SumAsync() ?? 0` or materialize first.
- **EF Core SQLite + enum string conversion**: Complex query chains with string-converted enums can fail EF Core translation. Materializing with `ToListAsync()` first is safer for dashboard-style queries.
- **Go DEM memory**: Large tracks spanning many SRTM tiles can use significant memory. Use `--dem-max-memory` flag or preload handles memory checks.

## E2E Testing — Playwright

The client has a Playwright E2E test suite with **full API mocking** (no backend required). Tests intercept all `/api/*` requests and return fixture JSON data.

### Running E2E Tests

```bash
cd ui/client
npm run build            # Required: tests run against preview build
npm run e2e              # All tests (desktop + mobile, ~102 tests)
npm run e2e:desktop      # Desktop only (Desktop Chrome 1280×720)
npm run e2e:mobile       # Mobile only (iPhone 14 viewport, Chromium)
npm run e2e:report       # Open HTML report
```

The `webServer` config in `playwright.config.ts` auto-starts `npm run preview` (port 4173).

### Test Structure

```
ui/client/e2e/
├── fixtures/          → JSON mock data (dashboard, activities, settings, etc.) + test.gpx
├── helpers/
│   └── mock-api.ts    → Intercepts all API routes, returns fixtures
├── dashboard.spec.ts, activities.spec.ts, activity-detail.spec.ts
├── upload.spec.ts, settings.spec.ts, integrations.spec.ts
├── navigation.spec.ts, i18n.spec.ts, pwa.spec.ts
```

### Key Patterns

- Every test calls `mockAllApi(page)` in `beforeEach` — no backend dependency
- `mock-api.ts` uses `fs.readFileSync` for fixture loading (ESM compatibility)
- Two projects: **desktop** (Desktop Chrome) and **mobile** (iPhone 14 viewport on Chromium)
- MapLibre: tests check container presence, not WebGL rendering (headless limitation)
- i18n tests clear `localStorage('i18nextLng')` via `addInitScript` and use `waitForLoadState('networkidle')` to handle Suspense
- Outputs (`e2e-results/`, `e2e-report/`) are gitignored

### After UI Changes

Run `npm run e2e` after significant UI modifications to catch regressions. If a test fails, check the screenshot in `e2e-results/` and the HTML report via `npm run e2e:report`.

## Environment

- .NET 9.0, Node 22+, React 19, Vite 7, TypeScript 5.9
- For local scripts and system operations, use **PowerShell** (Python is not installed)
- After a modification or an addition on the source code, rebuild and test the modified component.
- After a modification in the backend, use ef core migrations for database changes, and apply it to the current compose deployment once the feature finished
- After changes, redeploy on compose (`docker compose up --build -d`) for the user to test.
- At the end of a new feature, suggest to tracked only added or modified in this feature and in a second step to commit your work. Propose a commit message without git commit yourself.
