# Stage 1: Builder
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /build

# Copier la solution et les fichiers de projet
COPY *.slnx ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY NuGet.config ./

# Copier les projets
COPY src/libs ./src/libs
COPY src/apps/Lama.Server ./src/apps/Lama.Server

# Restaurer et publier
RUN dotnet restore src/apps/Lama.Server/Lama.Server.csproj
RUN dotnet publish -c Release -o /publish src/apps/Lama.Server/Lama.Server.csproj

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Outil requis par les healthchecks Docker/Compose
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copier les fichiers publiés depuis le builder
COPY --from=builder /publish .

# Copier les assets de langues
COPY assets/languages ./assets/languages

# Exposer le port
EXPOSE 5000

# Variables d'environnement
ENV ASPNETCORE_URLS="http://+:5000" \
    ASPNETCORE_ENVIRONMENT="Production"

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Point d'entrée
ENTRYPOINT ["dotnet", "Lama.Server.dll"]

