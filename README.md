# GPX Utility Analyzer

[![CI](https://github.com/jchable/gpx-utility-analyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/jchable/gpx-utility-analyzer/actions/workflows/ci.yml)
[![CodeQL](https://github.com/jchable/gpx-utility-analyzer/actions/workflows/codeql.yml/badge.svg)](https://github.com/jchable/gpx-utility-analyzer/actions/workflows/codeql.yml)
[![Docs](https://github.com/jchable/gpx-utility-analyzer/actions/workflows/deploy-docs.yml/badge.svg)](https://jchable.github.io/gpx-utility-analyzer/)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/jchable/gpx-utility-analyzer?include_prereleases&sort=semver)](https://github.com/jchable/gpx-utility-analyzer/releases)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

A full-stack platform for analyzing, processing and visualizing GPX activity files. Combines a .NET CLI for track statistics, AI-powered analysis for intelligent reports, and a web application with an interactive sport dashboard.

**Published online documentation**: [https://jchable.github.io/gpx-utility-analyzer/](https://jchable.github.io/gpx-utility-analyzer/)

## Projects

### [cli/](cli/) — gpx-analyzer (.NET)

.NET CLI tool for analyzing GPX files: distance, elevation gain/loss, speed, stop detection, biometrics (heart rate, power, cadence, temperature from GPX extensions), time-based splitting, file merging. Includes altitude correction using a digital elevation model (SRTM). Built as a Native AOT single-file executable with no runtime dependency.

**Install (Windows)** — available on winget:

```powershell
winget install Coderise.gpx-analyzer
```

Or build and run from source:

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

- **Multi-user**: ASP.NET Identity, JWT Bearer auth, role-based access (Admin, Premium, User)
- **Activity management**: upload GPX, list/view/delete activities, download GPX files, re-analyze
- **Dashboard**: aggregated statistics (total distance, elevation, time, activity breakdown)
- **Background processing**: async pipeline via `Channel<Guid>` — GPX analysis then AI report generation
- **Storage abstraction**: local filesystem (dev) or RustFS/S3-compatible object storage (prod)
- **Email service**: NoOp (dev) or SMTP via MailKit (prod)
- **External integrations**: Strava / Garmin + webhook for automatic activity import
- **Dual database**: SQLite (development) / PostgreSQL (production) via EF Core

```bash
dotnet run --project ui/api/GpxAnalyzer.Api.csproj    # http://localhost:5000
```

##### Local configuration

`appsettings.json` is committed without secrets. For local development, copy the example and fill in your own values:

```bash
cp ui/api/appsettings.Development.json.example ui/api/appsettings.Development.json
```

Required values:
- `Jwt:Secret` — random string ≥32 characters (used to sign JWTs)
- `AiProvider:ApiKey` — API key for the chosen provider (Gemini, OpenAI, Anthropic, etc.)
- `Integrations:Strava:ClientId` / `ClientSecret` — only if you use Strava import

`appsettings.Development.json` is gitignored. In production, prefer environment variables (`Jwt__Secret`, `AiProvider__ApiKey`, …) over a file.

For Docker, create a `.env` file at the repo root:

```env
JWT_SECRET=<random-string-of-at-least-32-characters>
AI_API_KEY=<your-ai-provider-api-key>
AI_MODEL=gemini-2.5-flash
STRAVA_CLIENT_ID=
STRAVA_CLIENT_SECRET=
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

Development (SQLite + local storage):

```bash
docker compose up --build
# API on :5000, client on :8081, RustFS console on :9001
```

Production (PostgreSQL):

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
# API on :5000, client on :80, PostgreSQL on :5432
```

To enable RustFS S3 object storage, add these env vars to the `api` service in `docker-compose.yml`:

```yaml
- Storage__Type=s3
- Storage__S3__Endpoint=http://rustfs:9000
- Storage__S3__AccessKey=rustfsadmin
- Storage__S3__SecretKey=rustfsadmin
- Storage__S3__BucketName=gpx-files
```

The API image is a 2-stage build: .NET publish → ASP.NET runtime.

See [Web App docs](https://jchable.github.io/gpx-utility-analyzer/docs/web-app) for the full deployment and configuration reference.

## Tech Stack

| Layer | Technologies |
|-------|-------------|
| CLI | .NET 9, Native AOT, System.CommandLine, SRTM/HGT |
| AI Analysis | .NET 9, Microsoft.Extensions.AI, multi-provider (Azure OpenAI, OpenAI, Anthropic, Mistral, Ollama, Gemini) |
| API | ASP.NET Core 9, EF Core (SQLite/PostgreSQL), ASP.NET Identity + JWT, AWSSDK.S3, MailKit |
| Frontend | React 19, TypeScript 5.9, Vite 7, TailwindCSS v4, MapLibre GL JS, Recharts, TanStack Query |
| Storage | Local filesystem (dev) / RustFS S3-compatible (prod) |
| Infra | Docker Compose, nginx, GitHub Actions |

## Contributing

Contributions are welcome! Please read the [Contributing Guide](CONTRIBUTING.md) to get
started — it covers the development setup, build/test commands, coding and commit
conventions, and the DCO sign-off (`git commit -s`) required on every commit.

By participating you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Community & Support

- 💬 Questions and ideas: [GitHub Discussions](https://github.com/jchable/gpx-utility-analyzer/discussions)
- 🐛 Bugs and features: [open an issue](https://github.com/jchable/gpx-utility-analyzer/issues/new/choose)
- 🔒 Security: see [SECURITY.md](SECURITY.md) — please report vulnerabilities privately
- ❓ Getting help: see [SUPPORT.md](SUPPORT.md)

Changes are tracked in the [CHANGELOG](CHANGELOG.md).

## License

This project is licensed under the **GNU Affero General Public License v3.0** — see the
[LICENSE](LICENSE) file for details.
