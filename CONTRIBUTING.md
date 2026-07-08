# Contributing to GPX Utility Analyzer

First off, thank you for taking the time to contribute! This project is a mono-repo
combining a .NET CLI, an AI analyzer, an ASP.NET Core API and a React front-end. This
guide explains how to get set up, the conventions we follow, and how to submit changes.

By participating, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Table of Contents

- [Ways to contribute](#ways-to-contribute)
- [Developer Certificate of Origin (DCO)](#developer-certificate-of-origin-dco)
- [Development setup](#development-setup)
- [Building & testing](#building--testing)
- [Coding conventions](#coding-conventions)
- [Commit conventions](#commit-conventions)
- [Database migrations](#database-migrations)
- [Submitting a pull request](#submitting-a-pull-request)
- [Reporting bugs & requesting features](#reporting-bugs--requesting-features)
- [License](#license)

## Ways to contribute

- **Report bugs** and **request features** via [GitHub Issues](../../issues).
- **Improve documentation** (READMEs, the Docusaurus site under `docs/`).
- **Fix bugs / implement features** — please open or comment on an issue first so we can
  agree on the approach before you invest significant time.

For questions and ideas, prefer [GitHub Discussions](../../discussions) over issues.

## Developer Certificate of Origin (DCO)

This project uses the [Developer Certificate of Origin](https://developercertificate.org/).
It is a lightweight way for contributors to certify that they wrote, or have the right to
submit, the code they contribute. You certify the DCO by **signing off** each commit:

```bash
git commit -s -m "feat(cli): add new elevation algorithm"
```

This appends a `Signed-off-by: Your Name <your@email.com>` trailer to the commit message.
Make sure your `user.name` and `user.email` are configured in git. A CI check enforces the
sign-off on every commit in a pull request.

> Forgot to sign off? Amend with `git commit --amend -s` (single commit) or
> `git rebase --signoff HEAD~N` (multiple commits), then force-push your branch.

## Development setup

### Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0+ |
| [Node.js](https://nodejs.org/) | 22+ |
| [Docker](https://www.docker.com/) (optional, for full-stack) | recent |

### Clone

```bash
git clone https://github.com/jchable/gpx-utility-analyzer.git
cd gpx-utility-analyzer
```

## Building & testing

After any change, **rebuild and test the component you touched.**

### CLI — `cli/`

```bash
cd cli
dotnet build src/GpxAnalyzer.Cli/
dotnet test tests/GpxAnalyzer.Cli.Tests/
```

### AI analyzer — `ai-analyzer/`

```bash
dotnet build ai-analyzer/src/GpxAiAnalyzer/GpxAiAnalyzer.csproj
dotnet test ai-analyzer/tests/GpxAiAnalyzer.Tests/
```

### API — `ui/api/`

```bash
dotnet build ui/api/GpxAnalyzer.Api.csproj
dotnet test ui/api.Tests/GpxAnalyzer.Api.Tests.csproj
```

### Client — `ui/client/`

```bash
cd ui/client
npm ci
npm run lint
npm run build

# End-to-end tests (Playwright, fully mocked — no backend needed)
npm run build && npm run e2e
```

### Full stack (Docker)

```bash
docker compose up --build      # API :5000, client :8081
```

## Coding conventions

- **C#**: follow the existing style (nullable enabled, file-scoped namespaces where used,
  `CultureInfo.InvariantCulture` for any string sent to an AI provider). Keep configuration
  in the existing `*Config` objects rather than adding new constructor parameters.
- **TypeScript/React**: pass `npm run lint` (ESLint). All user-facing strings must be
  internationalized via `react-i18next` — no hard-coded English/French text. Add keys to
  every locale (`en`, `fr`) under the correct namespace.
- **API errors**: return structured error codes (e.g. `{ "code": "NO_FILE_PROVIDED" }`),
  never localized message strings. The front-end localizes them.
- Keep changes focused; match the surrounding code's naming and comment density.

## Commit conventions

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary>
```

Common types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `perf`, `ci`, `build`.
Scopes are typically the sub-project: `cli`, `ai`, `api`, `client`, `docs`, `installer`,
`winget`, etc.

Examples:

```
feat(cli): add douglas-peucker elevation algorithm
fix(api): guard SumAsync against empty result sets on SQLite
docs(readme): document RustFS S3 configuration
```

Remember to sign off (`-s`) every commit — see [DCO](#developer-certificate-of-origin-dco).

## Database migrations

The API uses EF Core with dual SQLite/PostgreSQL support. **Never edit migration `.cs`
files by hand.**

```bash
# Add a migration
dotnet ef migrations add <Name> --project ui/api/GpxAnalyzer.Api.csproj

# Remove the last migration
dotnet ef migrations remove --project ui/api/GpxAnalyzer.Api.csproj

# Check for pending model changes
dotnet ef migrations has-pending-model-changes --project ui/api/GpxAnalyzer.Api.csproj
```

Watch out for the SQLite limitations documented in `CLAUDE.md` (no `DateTimeOffset` in
`ORDER BY`/`WHERE`, `SumAsync` on empty sets, enum-string query translation).

## Submitting a pull request

1. Fork the repo and create a topic branch from `main` (e.g. `feat/my-feature`).
2. Make your changes with signed-off, conventional commits.
3. Rebuild and test the affected component(s).
4. Update documentation (README / `docs/`) if behavior changed.
5. Open a PR against `main`, fill in the PR template, and link the related issue.
6. Ensure the CI checks (build, tests, lint, DCO) are green.

A maintainer will review your PR. Please be responsive to feedback — small, focused PRs
are reviewed and merged fastest.

## Reporting bugs & requesting features

Use the [issue templates](../../issues/new/choose). Include reproduction steps, the
affected component, versions, and (for the CLI) a minimal GPX sample if possible.

For **security vulnerabilities**, do **not** open a public issue — follow
[SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be licensed under the project's
[GNU Affero General Public License v3.0](LICENSE).
