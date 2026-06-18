# Lama.Server (alpha)

Serveur central autoritaire pour le mode multijoueur en ligne de commande.

## Objectif

- centraliser les parties en ligne,
- preparer le classement mondial,
- garder le mode local hors-ligne intact pour dev/test.

## Endpoints MVP

- `GET /health`
- `GET /health/db`
- `POST /api/games`
- `GET /api/games`
- `POST /api/games/{gameId}/join`
- `POST /api/games/{gameId}/moves`
- `GET /api/games/{gameId}`
- `GET /api/games/{gameId}/events` (SSE)
- `POST /api/games/{gameId}/end`
- `POST /internal/shutdown` (dev/test uniquement, active avec `LAMA_SERVER_ALLOW_SHUTDOWN=true`)

Notes:
- Les commandes de jeu online (`play.move`, `play.pass`, etc.) transitent via `POST /api/games/{gameId}/moves`.
- L'historique online est lu depuis `GET /api/games/{gameId}` (`moves`).
- `GET /api/games` fonctionne en mode hybride: fusion memoire + fallback lecture EF (`sessions.games`) pour les parties absentes en memoire.
- `GET /api/games/{gameId}` fonctionne en mode hybride: priorite state memoire, fallback lecture EF (`sessions.games`) si absent en memoire.

## Lancer en local

```bash
dotnet run --project src/Server/Lama.Server
```

Par defaut le serveur ecoute sur les URLs configurees par ASP.NET.

## EF Core (phase 2)

```bash
dotnet tool run dotnet-ef migrations list \
  --project src/Server/Lama.Server/Lama.Server.csproj \
  --startup-project src/Server/Lama.Server/Lama.Server.csproj \
  --context LamaDbContext

dotnet tool run dotnet-ef database update \
  --project src/Server/Lama.Server/Lama.Server.csproj \
  --startup-project src/Server/Lama.Server/Lama.Server.csproj \
  --context LamaDbContext
```

## Exemple rapide

```bash
curl -s http://localhost:5000/health

curl -s -X POST http://localhost:5000/api/games \
  -H "Content-Type: application/json" \
  -d '{"hostName":"Alice","gameLevel":"Standard"}'
```

## Notes

- Phase 1 EF Core activee: `LamaDbContext` + provider PostgreSQL + endpoint `GET /health/db`.
- Les endpoints gameplay restent en memoire dans `GameHubState` pour cette etape.
- Le mode local CLI reste disponible et isole des parties/classements en ligne.

