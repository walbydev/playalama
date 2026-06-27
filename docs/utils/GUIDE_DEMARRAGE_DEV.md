# Guide de démarrage et de déploiement

**Date** : 2026-06-19
**Statut** : En vigueur

---

Ce guide décrit les 7 cas d'usage pour développer, tester et déployer le projet LAMA.
Chaque cas dispose d'une **cible Makefile** et, pour les cas de debug, d'une **configuration Rider** partagée.

## Prérequis

| Outil | Version minimale | Obligatoire pour |
|-------|-----------------|-----------------|
| .NET SDK | 10.0 | tous les cas |
| Docker + Compose | 24+ / v2 | cas 2, 4, 5 |
| rsync | 3.x | cas 6, 7 |
| make | GNU make | raccourcis Makefile |
| Rider | 2024.x+ | cas 1, 2 (debug) |

Clé SSH configurée vers le VPS pour les cas 6 et 7 :

```bash
export LAMA_DEPLOY_SSH_KEY=~/.ssh/machines/playalama.key
```

---

## Cas 1 — Debug CLI en local (même PC)

**Quand l'utiliser** : développer/déboguer les commandes CLI sans serveur ni Docker.  
La partie est gérée entièrement en local via `JsonGameRepository`.

### Via Rider
Sélectionner la configuration **`🎮 Console - local debug`** dans la barre d'exécution.  
Modifier les `PROGRAM_PARAMETERS` selon la commande à tester (ex : `game show`, `play move H8 LAMA H`).

### Via terminal
```bash
make dev-local ARGS="game create Alice"
make dev-local ARGS="play move H8 LAMA H"
make dev-local ARGS="game show"
```

**Variables d'environnement utilisées** :

| Variable | Valeur | Rôle |
|----------|--------|------|
| `LAMA_RUNTIME_MODE` | `local` | Mode local (pas de HTTP) |
| `LAMA_SESSION_DIR` | `.dev-session/local` | Session isolée du vrai `~/.config/lama` |

---

## Cas 2 — Debug serveur + console en mode online (Rider + local)

**Quand l'utiliser** : développer/déboguer le flux online complet (CLI → HTTP → Server) avec points d'arrêt dans les deux projets.

### Architecture de débogage recommandée

```
Rider (debug)              Rider (debug)
┌─────────────┐   HTTP    ┌──────────────────┐
│ Console     │ ────────► │  Lama.Server     │
│ online mode │  :5201    │  Development     │
└─────────────┘           └──────────────────┘
```

> **Astuce** : Rider permet des points d'arrêt simultanés dans Console et Server.
> Utiliser la configuration compound **`🔗 COMPOUND - Server + Console online`**.

### Via Rider
1. Sélectionner **`🔗 COMPOUND - Server + Console online`** et cliquer sur **Debug** (pas Run).
2. Rider démarre les deux processus ; les points d'arrêt fonctionnent dans les deux.

### Manuellement (deux terminaux)
```bash
# Terminal 1 — Server
make dev-server

# Terminal 2 — Console (une fois le server démarré)
make dev-local ARGS="game create Alice --level casual"
# Puis adapter LAMA_RUNTIME_MODE=online manuellement, ou :
LAMA_RUNTIME_MODE=online LAMA_SERVER_URL=http://127.0.0.1:5201 \
  LAMA_SESSION_DIR=/tmp/lama-debug-host \
  dotnet run --project src/apps/Lama.Console -- game create Alice --level casual
```

### Debug avec infrastructure Docker (nginx autour du serveur Rider)

Pour tester nginx en local **sans mettre le serveur dans Docker** :

```bash
tools/scripts/docker-local.sh nginx-only
# → nginx démarre sur :80, proxifie vers :5201
# → démarrer Lama.Server depuis Rider (config Server - dev)
```

---

## Cas 3 — Exécution locale simple (sans Rider)

**Quand l'utiliser** : jouer une partie locale en mode normal, sans débogueur.

```bash
# Partie locale complète
make run-local ARGS="game create Alice"
make run-local ARGS="play move H8 LAMA H"
make run-local ARGS="game show"

# Raccourci alias (si configuré, voir docs/utils/ALIAS_LAMA_CONFIG.md)
lama game create Alice
```

---

## Cas 4 — Site web local via Docker

**Quand l'utiliser** : tester `site/static/` dans nginx local (CSS, JS, pages download, thème).  
Le serveur de jeu n'est pas nécessaire pour cela.

```bash
# Démarrer nginx seul
make docker-site-local
# → http://localhost/          site statique
# → http://localhost/download/ page téléchargements

# Arrêter
make docker-site-local-stop
```

Fichier de composition utilisé : `tools/docker/docker-compose.local.yml` (service `nginx` uniquement).

---

## Cas 5 — Stack Docker locale complète (serveur + nginx)

**Quand l'utiliser** : reproduire l'environnement production localement (intégration complète).

```bash
# Démarrer (build inclus)
make docker-local

# Vérifier
make docker-local-ps
make health-local
# → http://localhost/         site
# → http://localhost:5201/health   serveur

# Rebuild après modification
make docker-local-rebuild

# Logs
make docker-local-logs

# Arrêter
make docker-local-stop
```

### Via Rider
Sélectionner **`🐳 Docker local - stack complète`** dans la barre d'exécution.

---

## Cas 6 — Déploiement site web sur VPS production

**Quand l'utiliser** : mettre à jour `site/static/` (page d'accueil, CSS, JS, page download).  
N'affecte **pas** le serveur de jeu.

```bash
# Déploiement réel
make deploy-site-prod SSH_KEY=~/.ssh/machines/playalama.key

# Ou avec la variable d'env
export LAMA_DEPLOY_SSH_KEY=~/.ssh/machines/playalama.key
make deploy-site-prod

# Simulation préalable (recommandée)
make deploy-site-prod-dry SSH_KEY=~/.ssh/machines/playalama.key
```

**Ce qui est déployé** :

| Source locale | Destination VPS |
|---------------|----------------|
| `site/static/` | `/opt/playalama/site/static/` |
| `tools/docker/nginx-playalama.conf` | `/opt/playalama/tools/docker/nginx-playalama.conf` |
| `tools/docker/docker-compose.prod.yml` | `/opt/playalama/docker-compose.yml` |
| `artifacts/zip/*.zip` | `/opt/playalama/artifacts/zip/` |

Script sous-jacent : `tools/deployments/deploy-static-site.sh --mode prod`

---

## Cas 7 — Déploiement serveur de jeu sur VPS production

**Quand l'utiliser** : déployer une nouvelle version de `Lama.Server` (correctifs, fonctionnalités).  
Le serveur Docker est **reconstruit et redémarré** sur le VPS.

```bash
# Simulation (conseillée avant un déploiement réel)
make deploy-server-prod-dry SSH_KEY=~/.ssh/machines/playalama.key

# Déploiement réel
make deploy-server-prod SSH_KEY=~/.ssh/machines/playalama.key

# Déploiement complet (site + serveur en une commande)
make deploy-all-prod SSH_KEY=~/.ssh/machines/playalama.key
```

**Étapes du script** :
1. `dotnet build` — vérification complète
2. `dotnet publish -c Release` — publication dans `.deploy/stage/`
3. `rsync` — envoi des artefacts vers `/opt/playalama/lamaserver/publish/`
4. `docker compose build gameserver && up -d` — rebuild et redémarrage sur VPS
5. `curl /health` — vérification finale

Script sous-jacent : `tools/deployments/deploy-vps.sh`

---

## Vue d'ensemble des configurations Rider

Les configurations suivantes sont **partagées dans le dépôt** (`.idea/.idea.Lama/.idea/runConfigurations/`) et apparaissent automatiquement dans Rider :

| Nom dans Rider | Cas | Type |
|----------------|-----|------|
| `🎮 Console - local debug` | 1 | .NET Project |
| `🖥️ Server - dev (local)` | 2 | .NET Project |
| `🌐 Console - online (serveur local)` | 2 | .NET Project |
| `🔗 COMPOUND - Server + Console online` | 2 | Compound |
| `🐳 Docker local - stack complète` | 5 | Shell Script |

> **Note** : Les cas 6 et 7 (déploiements VPS) ne nécessitent pas de config Rider — les scripts
> sont pilotés depuis le Makefile ou un terminal externe.

---

## Récapitulatif rapide

```
Cas 1 — Debug CLI local         →  make dev-local ARGS="..."
                                   ou Rider : 🎮 Console - local debug

Cas 2 — Debug Server+Console    →  Rider : 🔗 COMPOUND - Server + Console online
                                   ou make dev-server + run manual CLI

Cas 3 — Run CLI local           →  make run-local ARGS="..."

Cas 4 — Site Docker local       →  make docker-site-local

Cas 5 — Stack Docker local      →  make docker-local
                                   ou Rider : 🐳 Docker local - stack complète

Cas 6 — Deploy site VPS         →  make deploy-site-prod SSH_KEY=~/.ssh/...

Cas 7 — Deploy serveur VPS      →  make deploy-server-prod SSH_KEY=~/.ssh/...
```

---

## Liens connexes

- `tools/docker/DOCKER_ARCHITECTURE.md` — architecture Docker détaillée
- `tools/docker/docker-compose.local.yml` — composition locale
- `tools/docker/docker-compose.prod.yml` — composition production (template VPS)
- `tools/deployments/deploy-vps.sh` — script déploiement serveur
- `tools/deployments/deploy-static-site.sh` — script déploiement site
- `docs/utils/ALIAS_LAMA_CONFIG.md` — configuration des alias CLI
