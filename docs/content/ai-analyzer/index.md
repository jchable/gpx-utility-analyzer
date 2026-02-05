---
title: "gpx-ai-analyzer — .NET Tool"
sidebar_label: "Overview"
sidebar_position: 1
slug: "/ai-analyzer"
---

# gpx-ai-analyzer

.NET CLI tool using Microsoft Agent Framework to produce intelligent
analysis reports (difficulty, key segments, recommendations) from
GPX statistics.

:::info Under development
This project is in early development. Documentation will be enriched
as development progresses.
:::

## Supported AI providers

- Azure OpenAI
- OpenAI
- Anthropic
- Ollama (local)

## Pipeline with gpx-analyzer

```bash
cli/gpx-analyzer analyze --format json track.gpx | ai-analyzer/gpx-ai-analyzer analyze --provider azure-openai
```
