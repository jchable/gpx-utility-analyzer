---
title: "Configuration Reference"
sidebar_label: "Configuration"
sidebar_position: 3
slug: "/web-app/configuration"
---

# Configuration Reference

All configuration is managed via ASP.NET Core's configuration system (`appsettings.json` + environment variables). Environment variable names use double-underscore (`__`) as the section separator.

## Full `appsettings.json`

```json
{
  "Database": {
    "Provider": "sqlite",
    "ConnectionStrings": {
      "Sqlite": "Data Source=data/gpxanalyzer.db",
      "PostgreSql": "Host=localhost;Database=gpxanalyzer;Username=gpx;Password=gpx"
    }
  },
  "Jwt": {
    "Secret": "<random-string-of-at-least-32-characters>",
    "Issuer": "gpx-analyzer",
    "Audience": "gpx-analyzer-client",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  },
  "Storage": {
    "Type": "local",
    "GpxDirectory": "data/gpx",
    "DemDirectory": "data/dem",
    "S3": {
      "Endpoint": "http://localhost:9000",
      "AccessKey": "rustfsadmin",
      "SecretKey": "rustfsadmin",
      "BucketName": "gpx-files",
      "BasePrefix": ""
    }
  },
  "Email": {
    "Type": "noop",
    "Smtp": {
      "Host": "localhost",
      "Port": 587,
      "UseSsl": false,
      "Username": "",
      "Password": "",
      "From": "noreply@gpx-analyzer.app",
      "FromName": "GPX Analyzer"
    }
  },
  "AiProvider": {
    "Name": "gemini",
    "Model": "gemini-2.0-flash",
    "ApiKey": "",
    "Endpoint": null
  },
  "GpxCli": {
    "DefaultPreset": "trail",
    "DefaultSmoothing": "medium",
    "DefaultTrackSmoothing": "medium"
  },
  "Routing": {
    "Provider": "",
    "Ors": {
      "ApiKey": "",
      "BaseUrl": "https://api.openrouteservice.org"
    },
    "Osrm": {
      "BaseUrl": "http://osrm:5000"
    }
  },
  "Integrations": {
    "Strava": {
      "ClientId": "",
      "ClientSecret": "",
      "WebhookVerifyToken": "gpx-analyzer"
    },
    "Garmin": {
      "ConsumerKey": "",
      "ConsumerSecret": ""
    }
  }
}
```

## Storage Modes

### Local Storage (default)

Files stored on the container filesystem. Suitable for single-instance dev/test deployments.

```json
{ "Storage": { "Type": "local" } }
```

No external dependencies. GPX files are archived as zip after first processing, then replaced by the enriched version.

### RustFS / S3 Storage

S3-compatible object storage. Recommended for production, enables horizontal scaling and durable storage.

```json
{
  "Storage": {
    "Type": "s3",
    "S3": {
      "Endpoint": "http://rustfs:9000",
      "AccessKey": "your-key",
      "SecretKey": "your-secret",
      "BucketName": "gpx-files"
    }
  }
}
```

The API auto-creates the S3 bucket on startup if it doesn't exist.

## AI Providers

| Name | Notes |
|------|-------|
| `gemini` | Google Gemini (default) — free tier available |
| `openai` | OpenAI GPT models |
| `anthropic` | Claude models |
| `mistral` | Mistral AI |
| `azure-openai` | Azure OpenAI — set `Endpoint` to your Azure endpoint |
| `ollama` | Local LLM via Ollama — set `Endpoint` to `http://ollama:11434` |

Leave `AiProvider:Name` empty or unset to disable AI reports (activities will still be analyzed).

## GPX CLI Settings

Per-user settings (overridable from the UI):

| Key | Default | Description |
|-----|---------|-------------|
| `GpxCli:DefaultPreset` | `trail` | Stop detection preset: `trail`, `hiking`, `run`, `walk`, `cycle`, `swim` |
| `GpxCli:DefaultSmoothing` | `medium` | Elevation smoothing: `none`, `light`, `medium`, `heavy` |
| `GpxCli:DefaultTrackSmoothing` | `medium` | Track (lat/lon) smoothing level |
| `GpxCli:AutoDetectActivityType` | `false` | Auto-detect activity type from GPX metadata and computed stats |
| `GpxCli:FixAnomalies` | `false` | Attempt to fix GPS anomalies (experimental) |

## Routing Services (optional)

For the route editor feature. Leave `Routing:Provider` empty to disable.

### OpenRouteService (ORS)

```json
{
  "Routing": {
    "Provider": "ors",
    "Ors": {
      "ApiKey": "your-ors-key",
      "BaseUrl": "https://api.openrouteservice.org"
    }
  }
}
```

### OSRM (self-hosted)

```json
{
  "Routing": {
    "Provider": "osrm",
    "Osrm": {
      "BaseUrl": "http://osrm:5000"
    }
  }
}
```

## Frontend Configuration

The React client is built at Docker image time. Runtime configuration is handled via API responses (language, units, etc.).

The Vite dev server (`npm run dev`) proxies `/api/*` requests to `http://localhost:5000`.
