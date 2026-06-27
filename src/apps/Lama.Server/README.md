# Lama.Server (alpha)

Serveur central autoritaire pour le mode multijoueur en ligne de commande.

## Objectif

- centraliser les parties en ligne,
- preparer le classement mondial,
- garder le mode local hors-ligne intact pour dev/test.

## Endpoints MVP

- `GET /health`
- `GET /health/db`
- `POST /api/v1/games`
- `GET /api/v1/games`
- `POST /api/v1/games/{gameId}/join`
- `POST /api/v1/games/{gameId}/moves`
- `GET /api/v1/games/{gameId}`
- `GET /api/v1/games/{gameId}/events` (SSE)
- `POST /api/v1/games/{gameId}/end`
- `POST /internal/shutdown` (dev/test uniquement, active avec `LAMA_SERVER_ALLOW_SHUTDOWN=true`)

Notes:
- Les commandes de jeu online (`play.move`, `play.pass`, etc.) transitent via `POST /api/v1/games/{gameId}/moves`.
- L'historique online est lu depuis `GET /api/v1/games/{gameId}` (`moves`).
- `GET /api/v1/games` fonctionne en mode hybride: fusion memoire + fallback EF (`sessions.games`) et, si present, comptage `sessions.players_in_game` / `sessions.turn_log`.
- `GET /api/v1/games/{gameId}` fonctionne en mode hybride: priorite state memoire, fallback EF metadata (`sessions.games`) + joueurs/coups/plateau (`sessions.players_in_game`, `sessions.turn_log`, `sessions.board_state`) quand disponibles.

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
curl -s http://localhost:5201/health

curl -s -X POST http://localhost:5201/api/v1/games \
  -H "Content-Type: application/json" \
  -d '{"hostName":"Alice","gameLevel":"Standard"}'
```

## Notes

- Phase 1 EF Core activee: `LamaDbContext` + provider PostgreSQL + endpoint `GET /health/db`.
- Les endpoints gameplay restent en memoire dans `GameHubState` pour cette etape.
- Le mode local CLI reste disponible et isole des parties/classements en ligne.
