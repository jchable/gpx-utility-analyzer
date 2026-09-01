# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-09-01

### Added

- **CLI**: characterization golden tests over the command layer — behavioural
  output, `--help` layout, parse errors — with every run sandboxed away from the
  developer's real SRTM cache (#135), and the 12 option defaults that
  System.CommandLine 2.x no longer prints now pinned by their observable effect.
- **Client**: Vitest unit-test harness.
- **Release**: a `/release` skill codifying the manual half of cutting a version.

### Changed

- **CLI**: command layer migrated to System.CommandLine 2.0.11 — `SetAction`,
  `parseResult.GetValue`, `DefaultValueFactory`; the pre-2.0 `SetHandler` and
  `InvocationContext` API is gone.
- **Build**: floating package versions pinned for reproducible builds (#133),
  and GitHub Actions bumped across the workflows.

### Fixed

- **CLI** — recording boundaries: `<trkseg>` boundaries are preserved through
  splitting, merging, benchmarking and every GPX this tool writes; a boundary is
  carried past the point `GpsFilter` drops (#142); each recording-boundary bit
  has a single owner; the `<trkseg>` hop is excluded from `total_distance_m` and
  `gpxa:dist` accumulates over the segments the pipeline actually counts (#144);
  recording gaps no longer count as activity metrics, GPS drift, or jitter.
- **CLI** — correctness and reporting: malformed input reports one line instead
  of a stack trace (#136); commands exit non-zero whenever they could not do what
  was asked (#139), and `analyze` no longer clobbers exports on failure; every
  stage affected by `--fix-anomalies` is recomputed and speeds re-clamped;
  contract timestamps use `InvariantCulture`; DEM neighbour tiles are no longer
  downloaded for an unreachable path.
- **API** — security and multi-user isolation: OAuth callbacks hardened and bound
  to the initiating user, expired OAuth states purged, webhooks routed to the
  owning user with the request body validated, the webhook secret made mandatory
  at startup and rejected at save time when missing (#143), token refresh refused
  for deactivated accounts, and the imported-activity unique index scoped per
  user.
- **API** — processing: expired processing leases reclaimed at runtime rather
  than only at startup, activity deletion survives a concurrent processing run
  (#131), timestamps stored in UTC, and stranded activities recovered.
- **API** — splits: elevation gain allocated across kilometre splits without
  double-counting boundary segments (#116).
- **Client**: token refresh made single-flight with stale responses ignored;
  race-plan and route editor fixes (complete plan sent on every PUT, editor stays
  dirty when edits land during an auto-save, cutoffs beyond 24 hours, polyline
  index translated to waypoint order); route exports downloaded with the bearer
  token; upload queue keyed by id rather than array position.
- **AI analyzer**: `ProviderOptions.Model` honoured for anthropic and mistral;
  JSON extracted by its braces with an empty response diagnosed; `TrackReport`
  deserialization made null-safe and genuinely lenient; non-zero exit when no
  input is provided.
- **CLI + API**: biometrics preserved on export, and the power namespace now
  round-trips.
- **Build**: platform-native optional dependencies reconciled after `npm ci`,
  working around the npm lockfile bug that broke the rollup native binary.

## [0.1.1] - 2026-07-09

### Added

- **Project health**: contributor governance files (`CONTRIBUTING.md`,
  `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`), `CODEOWNERS`, issue forms
  and a pull-request template, this `CHANGELOG.md`, funding metadata and README
  badges.
- **CI**: a build and test workflow covering the CLI, AI analyzer, API and React
  client, plus a DCO sign-off check on pull requests, and Dependabot coverage for
  NuGet, npm, GitHub Actions and Docker.
- **Docs**: AGPL-3.0 license, and winget install instructions for the CLI
  (`Coderise.gpx-analyzer`).

### Changed

- **winget**: package identifier set to `Coderise.gpx-analyzer`, manifests bumped
  to schema 1.12.0.
- **CI**: CodeQL moved to GitHub's default setup, and the APT repository update
  workflow removed.
- **E2E**: mocks and specs updated to match the current UI.

### Fixed

- **Installer**: registry values written under the 64-bit view (`SetRegView 64`),
  so the installed path is visible to 64-bit callers.
- **Release**: the winget submission uses the version without its leading `v`.
- **winget**: installer SHA256 corrected to match the rebuilt v0.1.0-alpha asset.
- **i18n**: repaired invalid `settings.json` for both English and French.
- **Docs**: broken links that were failing the Docusaurus build.
- **Client**: `npm audit fix` on the vite and workbox dependency trees
  (21 vulnerabilities down to 2).

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

[Unreleased]: https://github.com/jchable/gpx-utility-analyzer/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/jchable/gpx-utility-analyzer/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/jchable/gpx-utility-analyzer/compare/v0.1.0-alpha...v0.1.1
[0.1.0-alpha]: https://github.com/jchable/gpx-utility-analyzer/releases/tag/v0.1.0-alpha
