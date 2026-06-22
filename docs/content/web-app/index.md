---
title: "Web App — ASP.NET Core + React"
sidebar_label: "Overview"
sidebar_position: 1
slug: "/web-app"
---

# Web App — ASP.NET Core + React

The GPX Analyzer web application is a full-stack sport dashboard (Garmin Connect-style dark theme) built with:

- **Backend**: ASP.NET Core 9 Web API, Entity Framework Core, ASP.NET Identity + JWT
- **Frontend**: React 19, Vite 7, TailwindCSS v4, MapLibre GL JS
- **Database**: SQLite (dev) / PostgreSQL (prod)
- **Storage**: Local filesystem (dev) / RustFS S3-compatible (prod)
- **Background processing**: In-process channel + `BackgroundService`

## Architecture

```
ui/
├── api/          → ASP.NET Core Web API
│   ├── Controllers/         → HTTP endpoints
│   ├── Services/            → Business logic
│   │   ├── Storage/         → IStorageService, LocalStorageService, S3StorageService
│   │   ├── Email/           → IEmailService, SmtpEmailService, NoOpEmailService
│   │   └── ...
│   ├── Entities/            → EF Core entities
│   ├── Data/                → AppDbContext
│   └── Migrations/          → EF Core migrations
└── client/       → React frontend
    ├── src/
    │   ├── pages/           → Route-level components
    │   ├── components/      → Reusable UI components
    │   ├── api/client.ts    → Typed API client
    │   └── hooks/           → React Query hooks
    └── public/locales/      → i18n translation files (en, fr)
```

## Key Features

- **Multi-user**: ASP.NET Identity, JWT Bearer authentication, role-based access (`Admin`, `Premium`, `User`)
- **GPX Analysis**: In-process pipeline via `GpxAnalyzer.Cli.Core` (no subprocess)
- **AI Reports**: `GpxAiAnalyzer.Core` with support for OpenAI, Anthropic, Mistral, Gemini, Ollama
- **Storage abstraction**: swap local filesystem for RustFS/S3 with a config flag
- **PWA**: Service Worker with offline support (Workbox)
- **i18n**: English and French, auto-detected from browser

## Quick Start (Docker)

```bash
# Development (SQLite + local storage)
docker compose up --build

# Access
# Frontend: http://localhost:8081
# API:      http://localhost:5000
# RustFS console: http://localhost:9001 (if enabled)
```

See [Deployment](./deployment.md) and [Configuration](./configuration.md) for details.
