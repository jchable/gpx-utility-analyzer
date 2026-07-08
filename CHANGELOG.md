# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Open-source project health files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`,
  `SECURITY.md`, `SUPPORT.md`, `CODEOWNERS`, issue/PR templates.
- Continuous integration workflow (`ci.yml`) building and testing the CLI,
  AI analyzer, API, and React client, plus Playwright E2E.
- DCO sign-off check on pull requests (`dco.yml`).
- Dependabot configuration for NuGet, npm, GitHub Actions, and Docker.
- CodeQL static analysis for C# and JavaScript/TypeScript.

## [0.1.0-alpha] - 2026-06-19

### Added
- Initial public alpha release.
- **CLI** (`gpx-analyzer`): distance, elevation gain/loss, speed and stop
  statistics with SRTM DEM correction; `analyze`, `benchmark`, `split`, `merge`
  commands; Native AOT single-file build.
- **AI analyzer** (`gpx-ai-analyzer`): multi-provider AI reports (Azure OpenAI,
  OpenAI, Anthropic, Mistral, Ollama, Gemini) via Microsoft.Extensions.AI.
- **Web app**: ASP.NET Core API with background processing, EF Core
  (SQLite/PostgreSQL), ASP.NET Identity + JWT, Strava integration; React 19 +
  Vite dashboard (PWA) with map, elevation profile and AI report panels.
- **i18n**: English and French support across the front-end and AI reports.
- **Docs**: Docusaurus site deployed to GitHub Pages.
- Windows installer (NSIS) and winget manifest.

[Unreleased]: https://github.com/jchable/gpx-utility-analyzer/compare/v0.1.0-alpha...HEAD
[0.1.0-alpha]: https://github.com/jchable/gpx-utility-analyzer/releases/tag/v0.1.0-alpha
