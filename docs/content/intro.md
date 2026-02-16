---
title: "Overview"
sidebar_label: "Home"
sidebar_position: 0
slug: /
---

# GPX Utility Analyzer

A toolkit for analyzing, processing and transforming GPX files.

## Tools

### gpx-analyzer (.NET CLI)

.NET CLI tool for analyzing GPX files: distance, elevation gain/loss,
speed, stop detection, time-based splitting, file merging.
Includes altitude correction using a digital elevation model (SRTM).

[View documentation →](/gpx-utility-analyzer/docs/cli)

### gpx-ai-analyzer (.NET)

CLI tool using Microsoft Agent Framework to produce intelligent
analysis reports from GPX statistics. Supports multiple
AI providers (Azure OpenAI, OpenAI, Anthropic, Ollama).

[View documentation →](/gpx-utility-analyzer/docs/ai-analyzer)

## Pipeline

The two tools work together:

```bash
# CLI statistics → AI analysis
cli/gpx-analyzer analyze --format json track.gpx | ai-analyzer/gpx-ai-analyzer analyze --provider azure-openai
```
