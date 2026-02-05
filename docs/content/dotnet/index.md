---
title: "gpx-ai-analyzer — Outil .NET"
sidebar_label: "Présentation"
sidebar_position: 1
slug: "/dotnet"
---

# gpx-ai-analyzer

Outil CLI en .NET utilisant Microsoft Agent Framework pour produire des rapports
d'analyse intelligents (difficulté, segments clés, recommandations) à partir
des statistiques GPX.

:::info En cours de développement
Ce projet est en phase de développement initial. La documentation sera enrichie
au fur et à mesure de l'avancement.
:::

## Fournisseurs IA supportés

- Azure OpenAI
- OpenAI
- Anthropic
- Ollama (local)

## Pipeline avec gpx-analyzer

```bash
cli/gpx-analyzer analyze --format json track.gpx | dotnet/gpx-ai-analyzer analyze --provider azure-openai
```
