# Plan de migration multijoueur

**Date** : 2026-06-19
**Statut** : Planifié

---

## Decision produit

- **Online ranked**: serveur central autoritaire (`Lama.Server`).
- **Offline local**: mode local conserve, isole des parties et classements mondiaux.

## Principes

1. Le mode local doit fonctionner sans internet.
2. Les parties locales ne doivent pas impacter les classements mondiaux.
3. Les parties en ligne passent uniquement par le serveur central.
4. Les contrats metier (`GameResult`, `RankingQueue`) restent communs.

## Modes d'execution proposes

- `local` (defaut)
  - persistance locale JSON existante
  - classement local (optionnel), jamais publie
  - aucune dependance reseau

- `online`
  - partie creee/rejointe sur `Lama.Server`
  - evenements via SSE
  - ratings mondiaux (open/tournament/global)

## Variables d'environnement cible

- `LAMA_RUNTIME_MODE=local|online`
- `LAMA_SERVER_URL=http://localhost:5000`

## Phases

### Phase 0 (en cours)

- Bootstrap serveur central (`Lama.Server`) avec API de parties + SSE.
- Maintien du mode local actuel sans regression.

### Phase 1

- Introduire un `IOnlineGameGateway` cote CLI.
- Router `game.create/join/show/end` vers local ou online selon `LAMA_RUNTIME_MODE`.

### Phase 2

- Auth API (JWT) + comptes centralises.
- Persistences serveur (PostgreSQL) pour parties/ratings.

### Phase 3

- Matchmaking et supervision minimale (metrics, logs, backups).

## Criteres d'acceptation minimum

- `LAMA_RUNTIME_MODE=local` fonctionne sans internet.
- `LAMA_RUNTIME_MODE=online` cree/rejoint une partie via API distante.
- Une partie locale n'apparait jamais dans le leaderboard online.
