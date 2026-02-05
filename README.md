# GPX Utility Analyzer

Suite d'outils pour l'analyse, le traitement et la transformation de fichiers GPX.

## Projets

### [cli/](cli/) — gpx-analyzer (Go)

Outil CLI en Go pour analyser des fichiers GPX : distance, dénivelé, vitesse, détection d'arrêts, découpage temporel, fusion de fichiers. Inclut la correction d'altitude par modèle numérique de terrain (SRTM).

```bash
cd cli
go build -o gpx-analyzer .
gpx-analyzer analyze ma-rando.gpx
```

Voir [cli/README.md](cli/README.md) et [cli/docs/CLI_USAGE.md](cli/docs/CLI_USAGE.md) pour la documentation complète.

### [ai-analyzer/](ai-analyzer/) — gpx-ai-analyzer (.NET)

Outil CLI en .NET utilisant Microsoft Agent Framework pour produire des rapports d'analyse intelligents (difficulté, segments clés, recommandations) à partir des statistiques GPX. Supporte plusieurs fournisseurs IA (Azure OpenAI, OpenAI, Anthropic, Ollama).

```bash
# Pipeline : statistiques Go → analyse IA .NET
cli/gpx-analyzer analyze --format json track.gpx | ai-analyzer/gpx-ai-analyzer analyze --provider azure-openai
```

Voir [ai-analyzer/README.md](ai-analyzer/README.md) pour la documentation complète.
