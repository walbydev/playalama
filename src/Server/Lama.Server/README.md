# Lama.Server (alpha)

Serveur central autoritaire pour le mode multijoueur en ligne de commande.

## Objectif

- centraliser les parties en ligne,
- preparer le classement mondial,
- garder le mode local hors-ligne intact pour dev/test.

## Endpoints MVP

- `GET /health`
- `POST /api/games`
- `POST /api/games/{gameId}/join`
- `POST /api/games/{gameId}/moves`
- `GET /api/games/{gameId}`
- `GET /api/games/{gameId}/events` (SSE)

## Lancer en local

```bash
dotnet run --project src/Server/Lama.Server
```

Par defaut le serveur ecoute sur les URLs configurees par ASP.NET.

## Exemple rapide

```bash
curl -s http://localhost:5000/health

curl -s -X POST http://localhost:5000/api/games \
  -H "Content-Type: application/json" \
  -d '{"hostName":"Alice","gameLevel":"Standard"}'
```

## Notes

- Stockage en memoire (phase alpha) ; la persistance PostgreSQL est prevue dans l'etape suivante.
- Le mode local CLI reste disponible et isole des parties/classements en ligne.

