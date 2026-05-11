---
title: "Deployment"
sidebar_label: "Deployment"
sidebar_position: 2
slug: "/web-app/deployment"
---

# Deployment

## Development (Docker Compose)

The default `docker-compose.yml` starts the full stack with SQLite and local file storage — no external dependencies required.

```bash
# Clone and start
git clone https://github.com/jchable/gpx-utility-analyzer.git
cd gpx-utility-analyzer
docker compose up --build

# Rebuild after code changes
docker compose up --build -d
```

**Services started:**
| Container | Port | Description |
|-----------|------|-------------|
| `gpx-api` | 5000 | ASP.NET Core Web API |
| `gpx-client` | 8081 | React frontend (nginx) |
| `gpx-rustfs` | 9000 / 9001 | RustFS S3 storage (optional) |

The API auto-creates the SQLite database and runs migrations on startup.

## Production (PostgreSQL + RustFS)

Use the prod overlay to add PostgreSQL:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

To also enable S3 object storage, add these environment variables to the `api` service:

```yaml
- Storage__Type=s3
- Storage__S3__Endpoint=http://rustfs:9000
- Storage__S3__AccessKey=${RUSTFS_ACCESS_KEY}
- Storage__S3__SecretKey=${RUSTFS_SECRET_KEY}
- Storage__S3__BucketName=gpx-files
```

### RustFS (S3-compatible storage)

The `docker-compose.yml` includes a `rustfs` service. To activate it:

1. Uncomment the S3 env vars in the `api` service
2. Optionally set credentials via a `.env` file:

```env
RUSTFS_ACCESS_KEY=your-access-key
RUSTFS_SECRET_KEY=your-secret-key
```

Access the RustFS web console at `http://localhost:9001` (default credentials: `rustfsadmin` / `rustfsadmin`).

## Environment Variables

### Required for Production

| Variable | Description |
|----------|-------------|
| `Jwt__Secret` | JWT signing key (min 32 chars, keep secret) |
| `AiProvider__ApiKey` | API key for your AI provider |

### Database

| Variable | Default | Description |
|----------|---------|-------------|
| `Database__Provider` | `sqlite` | `sqlite` or `postgresql` |
| `Database__ConnectionStrings__Sqlite` | `Data Source=/app/data/gpxanalyzer.db` | SQLite path |
| `Database__ConnectionStrings__PostgreSql` | — | PostgreSQL connection string |

### Storage

| Variable | Default | Description |
|----------|---------|-------------|
| `Storage__Type` | `local` | `local` or `s3` |
| `Storage__GpxDirectory` | `/app/data/gpx` | Local GPX storage path |
| `Storage__DemDirectory` | `/app/data/dem` | SRTM DEM cache path |
| `Storage__S3__Endpoint` | `http://localhost:9000` | S3 endpoint URL |
| `Storage__S3__AccessKey` | `rustfsadmin` | S3 access key |
| `Storage__S3__SecretKey` | `rustfsadmin` | S3 secret key |
| `Storage__S3__BucketName` | `gpx-files` | S3 bucket name |

### Email

| Variable | Default | Description |
|----------|---------|-------------|
| `Email__Type` | `noop` | `noop` (logs only) or `smtp` |
| `Email__Smtp__Host` | `localhost` | SMTP server hostname |
| `Email__Smtp__Port` | `587` | SMTP port |
| `Email__Smtp__Username` | — | SMTP username |
| `Email__Smtp__Password` | — | SMTP password |
| `Email__Smtp__From` | `noreply@gpx-analyzer.app` | Sender address |

### AI Provider

| Variable | Default | Description |
|----------|---------|-------------|
| `AiProvider__Name` | `gemini` | Provider: `openai`, `anthropic`, `mistral`, `gemini`, `ollama`, `azure-openai` |
| `AiProvider__ApiKey` | — | API key |
| `AiProvider__Model` | — | Model name (e.g., `gemini-2.0-flash`) |
| `AiProvider__Endpoint` | — | Custom endpoint URL (Azure, Ollama) |

### JWT

| Variable | Default | Description |
|----------|---------|-------------|
| `Jwt__Secret` | Dev default | Signing key (≥32 chars) |
| `Jwt__Issuer` | `gpx-analyzer` | JWT issuer claim |
| `Jwt__Audience` | `gpx-analyzer-client` | JWT audience claim |
| `Jwt__AccessTokenExpirationMinutes` | `60` | Access token lifetime |
| `Jwt__RefreshTokenExpirationDays` | `30` | Refresh token lifetime |

## Data Volumes

| Volume | Description |
|--------|-------------|
| `api-data` | SQLite DB, local GPX files, DEM tiles |
| `rustfs-data` | RustFS object data |
| `rustfs-logs` | RustFS logs |

## Re-deploy Checklist

After backend code changes:

```bash
# 1. Rebuild and restart
docker compose up --build -d

# 2. Verify API is healthy
curl -s http://localhost:5000/api/activities -H "Authorization: Bearer <token>"

# 3. Check logs if needed
docker logs gpx-api --tail 50
docker logs gpx-client --tail 20
```
