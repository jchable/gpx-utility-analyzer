# ============================================
# Stage 1: Restore & Build .NET API
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src

# Copy project files for restore
COPY ai-analyzer/src/GpxAiAnalyzer.Core/GpxAiAnalyzer.Core.csproj ai-analyzer/src/GpxAiAnalyzer.Core/
COPY cli/src/GpxAnalyzer.Cli.Core/GpxAnalyzer.Cli.Core.csproj cli/src/GpxAnalyzer.Cli.Core/
COPY ui/api/GpxAnalyzer.Api.csproj ui/api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore ui/api/GpxAnalyzer.Api.csproj

# Copy source code
COPY ai-analyzer/src/GpxAiAnalyzer.Core/ ai-analyzer/src/GpxAiAnalyzer.Core/
COPY cli/src/GpxAnalyzer.Cli.Core/ cli/src/GpxAnalyzer.Cli.Core/
COPY ui/api/ ui/api/

# Publish
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish ui/api/GpxAnalyzer.Api.csproj -c Release -o /app/publish --no-restore

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install libicu for globalization (if needed)
RUN apt-get update && apt-get install -y --no-install-recommends libicu-dev && rm -rf /var/lib/apt/lists/*

# Copy published .NET app
COPY --from=dotnet-build /app/publish .

# Create data directories
RUN mkdir -p /app/data/gpx

# Environment
ENV ASPNETCORE_URLS=http://+:5000
ENV Storage__GpxDirectory=/app/data/gpx

EXPOSE 5000

ENTRYPOINT ["dotnet", "GpxAnalyzer.Api.dll"]
