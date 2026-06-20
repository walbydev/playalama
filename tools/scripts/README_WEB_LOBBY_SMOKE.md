# Web Lobby Smoke Test

Script: `tools/scripts/e2e-web-lobby-smoke.sh`

## Objectif

Valider rapidement le flux principal Web lobby:

1. accessibilité `WebApp` et `Server`
2. inscription d'un utilisateur
3. création d'une partie lobby
4. démarrage de la partie par l'hôte
5. appel `Mes parties` (`/api/v1/players/me/games`)
6. validation de la structure JSON `games[].queue`

## Prérequis

- `Lama.Server` démarré (par défaut `http://127.0.0.1:5201`)
- `Lama.WebApp` démarrée (par défaut `http://127.0.0.1:5202`)
- `python3` installé

## Exécution rapide

```bash
cd /home/philippe/RiderProjects/Games/Lama
./tools/scripts/e2e-web-lobby-smoke.sh
```

## Variables optionnelles

```bash
SERVER_URL=http://127.0.0.1:5201 WEBAPP_URL=http://127.0.0.1:5202 ./tools/scripts/e2e-web-lobby-smoke.sh
```

## Résultat attendu

Le script termine avec:

- `SUCCÈS: smoke test web lobby validé`
- l'identifiant `gameId` créé pour le test

