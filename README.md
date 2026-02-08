# GPX Utility Analyzer

A toolkit for analyzing, processing and transforming GPX files.

**Published online documentation**: [https://jchable.github.io/gpx-utility-analyzer/](https://jchable.github.io/gpx-utility-analyzer/)

## Projects

### [cli/](cli/) — gpx-analyzer (Go)

Go CLI tool for analyzing GPX files: distance, elevation gain/loss, speed, stop detection, time-based splitting, file merging. Includes altitude correction using a digital elevation model (SRTM).

```bash
cd cli
go build -o gpx-analyzer .
gpx-analyzer analyze my-hike.gpx
```

See [cli/README.md](cli/README.md) and [cli/docs/CLI_USAGE.md](cli/docs/CLI_USAGE.md) for complete documentation.

### [ai-analyzer/](ai-analyzer/) — gpx-ai-analyzer (.NET)

.NET CLI tool using Microsoft Agent Framework to produce intelligent analysis reports (difficulty, key segments, recommendations) from GPX statistics. Supports multiple AI providers (Azure OpenAI, OpenAI, Anthropic, Ollama).

```bash
# Pipeline: Go statistics → .NET AI analysis
cli/gpx-analyzer analyze --format json track.gpx | ai-analyzer/gpx-ai-analyzer analyze --provider azure-openai
```

See [ai-analyzer/README.md](ai-analyzer/README.md) for complete documentation.
