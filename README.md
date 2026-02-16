# GPX Utility Analyzer

A full-stack platform for analyzing, processing and visualizing GPX activity files. Combines a Go CLI for track statistics, a .NET AI analyzer for intelligent reports, and a web application with an interactive sport dashboard.

**Published online documentation**: [https://jchable.github.io/gpx-utility-analyzer/](https://jchable.github.io/gpx-utility-analyzer/)

## Projects

### [cli/](cli/) — gpx-analyzer (.NET)

.NET CLI tool for analyzing GPX files: distance, elevation gain/loss, speed, stop detection, biometrics (heart rate, power, cadence, temperature from GPX extensions), time-based splitting, file merging. Includes altitude correction using a digital elevation model (SRTM). Built as a Native AOT single-file executable with no runtime dependency.

```bash
cd cli
dotnet build src/GpxAnalyzer.Cli/
dotnet run --project src/GpxAnalyzer.Cli/ -- analyze my-hike.gpx
```

See [cli/README.md](cli/README.md) for build, publish and test instructions.

### [ai-analyzer/](ai-analyzer/) — gpx-ai-analyzer (.NET)

.NET CLI tool using Microsoft.Extensions.AI to produce intelligent analysis reports (difficulty, key segments, recommendations) from GPX statistics. Supports multiple AI providers (Azure OpenAI, OpenAI, Anthropic, Mistral, Ollama, Gemini).

```bash
# Pipeline: CLI statistics → .NET AI analysis
cli/gpx-analyzer analyze --format json track.gpx | ai-analyzer/gpx-ai-analyzer analyze --provider azure-openai
```

See [ai-analyzer/README.md](ai-analyzer/README.md) for complete documentation.

### [ui/](ui/) — Web Application

Full-stack web application with a sport dashboard UI inspired by Garmin Connect.

#### API — [ui/api/](ui/api/) (ASP.NET Core)

REST API that orchestrates GPX analysis and AI reports with background processing.

- **Activity management**: upload GPX, list/view/delete activities, download GPX files, re-analyze
- **Dashboard**: aggregated statistics (total distance, elevation, time, activity breakdown)
- **Background processing**: async pipeline via `Channel<Guid>` — GPX analysis then AI report generation
- **External integrations**: Strava / Garmin + webhook for automatic activity import
- **Dual database**: SQLite (development) / PostgreSQL (production) via EF Core

```bash
dotnet run --project ui/api/GpxAnalyzer.Api.csproj    # http://localhost:5000
```

#### Client — [ui/client/](ui/client/) (React)

Single-page application built with React 19, TypeScript 5.9, Vite 7 and TailwindCSS v4.

- **Dashboard**: summary stats with radial gauges, activity type breakdown (donut chart), recent activities
- **Activity detail**: interactive 3D map (MapLibre GL JS), elevation/speed/GAP profile chart (Recharts), AI analysis panel, biometric stats
- **Map views**: 3D terrain, 3D satellite, 2D OpenTopo — with DEM terrain exaggeration
- **Elevation chart**: elevation area + speed line + GAP (Grade Adjusted Pace via Minetti model), togglable layers
- **Live status**: auto-refresh during processing (Pending → Analyzing → AI Processing → Completed)
- **Activity types**: run, trail, hike, cycle, walk, swim — each with dedicated color and icon

```bash
cd ui/client
npm install
npm run dev      # http://localhost:5173 (proxies /api → :5000)
npm run build    # Production build
```

### [docs/](docs/) — Documentation (Docusaurus)

Project documentation deployed to GitHub Pages. Content synced from sub-project READMEs.

```bash
cd docs
npm install
npm run start
```

## Docker

Development (SQLite):

```bash
docker compose up --build
# API on :5000, client on :8080
```

Production (PostgreSQL):

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
# API on :5000, client on :80, PostgreSQL on :5432
```

The API image is a 2-stage build: .NET publish → ASP.NET runtime.

## Tech Stack

| Layer | Technologies |
|-------|-------------|
| CLI | .NET 9, Native AOT, System.CommandLine, SRTM/HGT |
| AI Analysis | .NET 9, Microsoft.Extensions.AI, multi-provider (Azure OpenAI, OpenAI, Anthropic, Mistral, Ollama, Gemini) |
| API | ASP.NET Core 9, EF Core (SQLite/PostgreSQL), Background Services |
| Frontend | React 19, TypeScript 5.9, Vite 7, TailwindCSS v4, MapLibre GL JS, Recharts, TanStack Query |
| Infra | Docker Compose, nginx, GitHub Actions |
