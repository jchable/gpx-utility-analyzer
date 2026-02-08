# ============================================
# Stage 1: Build Go CLI
# ============================================
FROM golang:1-alpine AS go-build
WORKDIR /src/cli
COPY cli/go.mod cli/go.sum ./
RUN go mod download
COPY cli/ ./
RUN CGO_ENABLED=0 GOOS=linux go build -o /out/gpx-analyzer .

# ============================================
# Stage 2: Restore & Build .NET API
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src

# Copy project files for restore
COPY ai-analyzer/src/GpxAiAnalyzer.Core/GpxAiAnalyzer.Core.csproj ai-analyzer/src/GpxAiAnalyzer.Core/
COPY ui/api/GpxAnalyzer.Api.csproj ui/api/
RUN dotnet restore ui/api/GpxAnalyzer.Api.csproj

# Copy source code
COPY ai-analyzer/src/GpxAiAnalyzer.Core/ ai-analyzer/src/GpxAiAnalyzer.Core/
COPY ui/api/ ui/api/

# Publish
RUN dotnet publish ui/api/GpxAnalyzer.Api.csproj -c Release -o /app/publish --no-restore

# ============================================
# Stage 3: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install libicu for globalization (if needed)
RUN apt-get update && apt-get install -y --no-install-recommends libicu-dev && rm -rf /var/lib/apt/lists/*

# Copy Go CLI binary
COPY --from=go-build /out/gpx-analyzer /usr/local/bin/gpx-analyzer
RUN chmod +x /usr/local/bin/gpx-analyzer

# Copy published .NET app
COPY --from=dotnet-build /app/publish .

# Create data directories
RUN mkdir -p /app/data/gpx

# Environment
ENV ASPNETCORE_URLS=http://+:5000
ENV GpxCli__BinaryPath=/usr/local/bin/gpx-analyzer
ENV Storage__GpxDirectory=/app/data/gpx

EXPOSE 5000

ENTRYPOINT ["dotnet", "GpxAnalyzer.Api.dll"]
