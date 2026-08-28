# AtlasNOC - Production Dockerfile
# Multi-stage build for minimal production image

# ─── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for dependency resolution
COPY ["AtlasNOC.csproj", "./"]
COPY ["AtlasNOC.Domain/AtlasNOC.Domain.csproj", "AtlasNOC.Domain/"]
COPY ["Tests/AtlasNOC.Domain.Tests/AtlasNOC.Domain.Tests.csproj", "Tests/AtlasNOC.Domain.Tests/"]

# Restore dependencies
RUN dotnet restore "AtlasNOC.csproj"

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish "AtlasNOC.csproj" -c Release -o /app/publish --no-restore

# ─── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd --gid 1000 atlasnoc && useradd --uid 1000 --gid atlasnoc --shell /bin/bash --create-home atlasnoc

# Copy published application
COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chown -R atlasnoc:atlasnoc /app

# Switch to non-root user
USER atlasnoc

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose ports
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Run
ENTRYPOINT ["dotnet", "AtlasNOC.dll"]