# gpx-ai-analyzer

.NET CLI tool for AI-powered intelligent analysis of GPX tracks. Consumes JSON output from `gpx-analyzer` (.NET CLI) and produces structured reports: difficulty, key segments, recommendations, effort estimation.

## Prerequisites

- .NET 9.0+
- A configured AI provider (Azure OpenAI, OpenAI, Anthropic, or local Ollama)

## Build

```bash
dotnet build src/GpxAiAnalyzer/GpxAiAnalyzer.csproj -c Release
```

## Tests

```bash
dotnet test tests/GpxAiAnalyzer.Tests/
```

## Usage

### Pipeline with the CLI

```bash
gpx-analyzer analyze --format json my-hike.gpx | gpx-ai-analyzer analyze --provider openai
```

### From a pre-computed JSON file

```bash
gpx-ai-analyzer analyze --provider azure-openai --input stats.json
```

### JSON output

```bash
gpx-ai-analyzer analyze --provider anthropic --input stats.json --format json
```

### Options

| Option | Required | Description |
|--------|----------|-------------|
| `--provider` | Yes | AI provider: `azure-openai`, `openai`, `anthropic`, `ollama` |
| `--input` | No | JSON file (reads stdin otherwise) |
| `--api-key` | No | API key (overrides environment variable) |
| `--endpoint` | No | Endpoint URL (overrides environment variable) |
| `--model` | No | Model name (provider-specific default) |
| `--format` | No | Output format: `text` (default) or `json` |

## Provider configuration

Each provider reads its parameters from CLI arguments, then falls back to environment variables:

| Provider | Environment variables | Default model |
|----------|----------------------|---------------|
| `azure-openai` | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` | `gpt-4o-mini` |
| `openai` | `OPENAI_API_KEY` | `gpt-4o-mini` |
| `anthropic` | `ANTHROPIC_API_KEY` | `claude-haiku-4-5` |
| `ollama` | `OLLAMA_ENDPOINT` (default: `http://localhost:11434`) | `llama3.1` |

## Adding a new provider

1. Create a class implementing `IChatClientProvider` in `Providers/`
2. Register in `Program.cs`: `registry.Register(new MyProvider());`
3. Add the SDK NuGet package

## Architecture

```text
src/GpxAiAnalyzer/
├── Program.cs              # Entry point, provider registration
├── Commands/               # CLI commands (System.CommandLine)
├── Models/                 # GpxStats (Go JSON contract), TrackReport (AI report)
├── Providers/              # IChatClientProvider + 4 implementations
├── Analysis/               # TrackAnalyzer, PromptBuilder, AnalysisTools
└── Output/                 # ReportFormatter (text/JSON)
```
