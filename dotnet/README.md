# gpx-ai-analyzer

Outil CLI en .NET pour l'analyse intelligente de traces GPX par IA. Consomme la sortie JSON de `gpx-analyzer` (CLI Go) et produit des rapports structurés : difficulté, segments clés, recommandations, estimation d'effort.

## Prérequis

- .NET 9.0+
- Un fournisseur IA configuré (Azure OpenAI, OpenAI, Anthropic, ou Ollama local)

## Build

```bash
dotnet build src/GpxAiAnalyzer/GpxAiAnalyzer.csproj -c Release
```

## Tests

```bash
dotnet test tests/GpxAiAnalyzer.Tests/
```

## Utilisation

### Pipeline avec le CLI Go

```bash
gpx-analyzer analyze --format json ma-rando.gpx | gpx-ai-analyzer analyze --provider openai
```

### Depuis un fichier JSON pré-calculé

```bash
gpx-ai-analyzer analyze --provider azure-openai --input stats.json
```

### Sortie JSON

```bash
gpx-ai-analyzer analyze --provider anthropic --input stats.json --format json
```

### Options

| Option | Requis | Description |
|--------|--------|-------------|
| `--provider` | Oui | Fournisseur IA : `azure-openai`, `openai`, `anthropic`, `ollama` |
| `--input` | Non | Fichier JSON (sinon lit stdin) |
| `--api-key` | Non | Clé API (override la variable d'environnement) |
| `--endpoint` | Non | URL du endpoint (override la variable d'environnement) |
| `--model` | Non | Nom du modèle (défaut spécifique au provider) |
| `--format` | Non | Format de sortie : `text` (défaut) ou `json` |

## Configuration des providers

Chaque provider lit ses paramètres depuis les arguments CLI, puis les variables d'environnement en fallback :

| Provider | Variables d'environnement | Modèle par défaut |
|----------|--------------------------|-------------------|
| `azure-openai` | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` | `gpt-4o-mini` |
| `openai` | `OPENAI_API_KEY` | `gpt-4o-mini` |
| `anthropic` | `ANTHROPIC_API_KEY` | `claude-haiku-4-5` |
| `ollama` | `OLLAMA_ENDPOINT` (défaut: `http://localhost:11434`) | `llama3.1` |

## Ajouter un nouveau provider

1. Créer une classe implémentant `IChatClientProvider` dans `Providers/`
2. Enregistrer dans `Program.cs` : `registry.Register(new MonProvider());`
3. Ajouter le package NuGet du SDK

## Architecture

```text
src/GpxAiAnalyzer/
├── Program.cs              # Point d'entrée, enregistrement providers
├── Commands/               # Commandes CLI (System.CommandLine)
├── Models/                 # GpxStats (contrat JSON Go), TrackReport (rapport IA)
├── Providers/              # IChatClientProvider + 4 implémentations
├── Analysis/               # TrackAnalyzer, PromptBuilder, AnalysisTools
└── Output/                 # ReportFormatter (texte/JSON)
```
