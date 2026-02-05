---
title: "Présentation"
sidebar_label: "Accueil"
sidebar_position: 0
slug: /
---

# GPX Utility Analyzer

Suite d'outils pour l'analyse, le traitement et la transformation de fichiers GPX.

## Les outils

### gpx-analyzer (CLI Go)

Outil en ligne de commande pour analyser des fichiers GPX : distance, dénivelé,
vitesse, détection d'arrêts, découpage temporel, fusion de fichiers.
Inclut la correction d'altitude par modèle numérique de terrain (SRTM).

[Voir la documentation →](/gpx-utility-analyzer/docs/cli)

### gpx-ai-analyzer (.NET)

Outil CLI utilisant Microsoft Agent Framework pour produire des rapports
d'analyse intelligents à partir des statistiques GPX. Supporte plusieurs
fournisseurs IA (Azure OpenAI, OpenAI, Anthropic, Ollama).

[Voir la documentation →](/gpx-utility-analyzer/docs/dotnet)

## Pipeline

Les deux outils fonctionnent ensemble :

```bash
# Statistiques Go → analyse IA .NET
cli/gpx-analyzer analyze --format json track.gpx | dotnet/gpx-ai-analyzer analyze --provider azure-openai
```
