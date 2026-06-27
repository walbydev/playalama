# =============================================================================
# LAMA — Makefile principal
# =============================================================================
# Usage : make <cible> [ARGS="..."] [SSH_KEY="~/.ssh/..."]
#
# Déploiement VPS (PROD + STAGING avec Traefik) :
#   setup-vps        Initialisation VPS (1 seule fois, idempotent, image-only)
#   clean-all-vps    ⚠ Remise à zéro COMPLÈTE du VPS (destructif)
#   deploy-traefik   (Re)déployer Traefik sur le VPS
#   deploy-prod      Déployer l'environnement PROD  (playalama.online)
#   deploy-staging   Déployer l'environnement STAGING (staging.playalama.online)
#   vps-status       Afficher l'état des containers sur le VPS
#   cleanup-vps      Nettoyer les anciens containers/images obsolètes
#
# Scénarios de démarrage local :
#   1. dev-local         Debug CLI en local (même PC, sans serveur)
#   2. dev-server        Serveur ASP.NET en mode développement (Rider ou terminal)
#   3. run-local         Exécution CLI locale simple (sans Rider)
#   4. docker-local      Déploiement + exécution stack complète en local (Docker)
#   5. dev-debug         Build + lancer Server+WebApp+AIServer en natif (DB Docker)
# =============================================================================

SHELL := /bin/bash
ROOT_DIR := $(shell pwd)
CONSOLE_PROJECT := src/apps/Lama.Console/Lama.Console.csproj
SERVER_PROJECT  := src/apps/Lama.Server/Lama.Server.csproj
WEBAPP_PROJECT  := src/apps/Lama.WebApp/Lama.WebApp.csproj
AISERVER_PROJECT := src/apps/Lama.AIServer/Lama.AIServer.csproj
DOCKER_LOCAL    := tools/docker/docker-compose.local.yml
DOCKER_DEBUG    := tools/docker/docker-compose.local-debug.yml

# SSH_KEY peut être surchargé : make deploy-server-prod SSH_KEY=~/.ssh/machines/playalama.key
SSH_KEY         ?= $(LAMA_DEPLOY_SSH_KEY)

# Arguments par défaut pour run-local
ARGS            ?= game create Alice

# =============================================================================
# 1. Debug CLI en local (même PC)
# =============================================================================
.PHONY: dev-local
dev-local: ## [Cas 1] Lancer le CLI en mode local (debug terminal)
	LAMA_RUNTIME_MODE=local \
	LAMA_SESSION_DIR=/tmp/lama-dev-session \
	dotnet run --project $(CONSOLE_PROJECT) -- $(ARGS)

# =============================================================================
# 2. Serveur ASP.NET en développement
# =============================================================================
.PHONY: dev-server
dev-server: ## [Cas 2] Serveur Lama en mode Development (hot-reload, swagger)
	ASPNETCORE_ENVIRONMENT=Development \
	LAMA_SERVER_ALLOW_SHUTDOWN=true \
	dotnet run --project $(SERVER_PROJECT) --urls http://127.0.0.1:5201

# =============================================================================
# 3. Exécution locale CLI (sans Rider)
# =============================================================================
.PHONY: run-local
run-local: ## [Cas 3] Exécuter une commande CLI locale  →  make run-local ARGS="game create Alice"
	LAMA_RUNTIME_MODE=local \
	dotnet run --project $(CONSOLE_PROJECT) -- $(ARGS)

# =============================================================================
# 4. Stack Docker locale complète (serveur + webapp)
# =============================================================================
.PHONY: docker-local
docker-local: ## [Cas 5] Démarrer stack complète locale (lama-server + nginx)
	docker compose -f $(DOCKER_LOCAL) up -d --build

.PHONY: docker-local-rebuild
docker-local-rebuild: ## Rebuild + redémarrage forcé de la stack locale
	docker compose -f $(DOCKER_LOCAL) up -d --build --force-recreate

.PHONY: docker-local-stop
docker-local-stop: ## Arrêter la stack locale
	docker compose -f $(DOCKER_LOCAL) down

.PHONY: docker-local-logs
docker-local-logs: ## Suivre les logs de la stack locale
	docker compose -f $(DOCKER_LOCAL) logs -f

# =============================================================================
# VPS PROD + STAGING — Déploiement Dockerisé avec Traefik
# =============================================================================
DEPLOY_TARGET ?= debian@playalama.online

.PHONY: setup-vps
setup-vps: ## [VPS] Initialisation du VPS (idempotent, image-only) — Docker, structure, Traefik
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make setup-vps SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/setup-vps.sh \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY)

.PHONY: setup-vps-dry
setup-vps-dry: ## [VPS] Simulation du setup VPS (dry-run)
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie."; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/setup-vps.sh \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

.PHONY: clean-all-vps
clean-all-vps: ## [VPS] ⚠ DESTRUCTIF — Remise à zéro complète (containers, images, volumes, répertoires)
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make clean-all-vps SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/clean-all-vps.sh \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY)

.PHONY: clean-all-vps-with-traefik
clean-all-vps-with-traefik: ## [VPS] ⚠ DESTRUCTIF — Remise à zéro TOTALE Traefik inclus (certs TLS perdus !)
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie."; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/clean-all-vps.sh \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY) \
	  --include-traefik

.PHONY: clean-all-vps-dry
clean-all-vps-dry: ## [VPS] Simulation remise à zéro complète (dry-run)
	bash tools/scripts/deploy/clean-all-vps.sh \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

.PHONY: deploy-traefik
deploy-traefik: ## [VPS] (Re)déployer Traefik sur le VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-traefik SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	scp -i $(SSH_KEY) tools/docker/traefik.yml                $(DEPLOY_TARGET):/opt/playalama/traefik/traefik.yml
	scp -i $(SSH_KEY) tools/docker/docker-compose.traefik.yml  $(DEPLOY_TARGET):/opt/playalama/traefik/docker-compose.yml
	scp -i $(SSH_KEY) tools/docker/nginx-docker-api-proxy.conf $(DEPLOY_TARGET):/opt/playalama/traefik/nginx-docker-api-proxy.conf
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /opt/playalama/traefik && docker compose pull && docker compose up -d"

.PHONY: deploy-prod
deploy-prod: ## [VPS] Déployer PROD → https://playalama.online
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-prod SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/deploy-env.sh \
	  --env prod \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY)

.PHONY: deploy-prod-dry
deploy-prod-dry: ## [VPS] Simulation déploiement PROD (dry-run)
	bash tools/scripts/deploy/deploy-env.sh \
	  --env prod \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

.PHONY: deploy-staging
deploy-staging: ## [VPS] Déployer STAGING → https://staging.playalama.online
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-staging SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/deploy-env.sh \
	  --env staging \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY)

.PHONY: deploy-staging-dry
deploy-staging-dry: ## [VPS] Simulation déploiement STAGING (dry-run)
	bash tools/scripts/deploy/deploy-env.sh \
	  --env staging \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

.PHONY: health-prod
health-prod: ## Vérifier l'état de PROD (https://playalama.online/health)
	@curl -fsS https://playalama.online/health && echo "✓ Prod OK" || echo "✗ Prod KO"

.PHONY: health-staging
health-staging: ## Vérifier l'état de STAGING (https://staging.playalama.online/health)
	@curl -fsS https://staging.playalama.online/health && echo "✓ Staging OK" || echo "✗ Staging KO"

.PHONY: logs-prod
logs-prod: ## Afficher les logs PROD en temps réel (lama-server-prod + lama-webapp-prod)
	@if [ -z "$(SSH_KEY)" ]; then echo "⚠  SSH_KEY non définie."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /opt/playalama/prod && docker compose -p prod logs -f --tail=100 lama-server-prod lama-webapp-prod"

.PHONY: logs-staging
logs-staging: ## Afficher les logs STAGING en temps réel (lama-server-staging + lama-webapp-staging)
	@if [ -z "$(SSH_KEY)" ]; then echo "⚠  SSH_KEY non définie."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /opt/playalama/staging && docker compose -p staging logs -f --tail=100 lama-server-staging lama-webapp-staging"

.PHONY: cleanup-vps
cleanup-vps: ## [VPS] Nettoyer les anciens containers/images obsolètes sur le VPS
	@if [ -z "$(SSH_KEY)" ]; then echo "⚠  SSH_KEY non définie."; exit 1; fi
	bash tools/scripts/deploy/cleanup-vps.sh --ssh-key $(SSH_KEY) --target $(DEPLOY_TARGET)

.PHONY: cleanup-vps-dry
cleanup-vps-dry: ## [VPS] Simulation nettoyage VPS (dry-run)
	bash tools/scripts/deploy/cleanup-vps.sh \
	  --ssh-key $(SSH_KEY) --target $(DEPLOY_TARGET) --dry-run

.PHONY: vps-status
vps-status: ## [VPS] Afficher l'état des containers sur le VPS
	@if [ -z "$(SSH_KEY)" ]; then echo "⚠  SSH_KEY non définie."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' && echo '' && docker images --format 'table {{.Repository}}\t{{.Tag}}\t{{.Size}}' | grep lama"

# =============================================================================
# Dev-debug : PostgreSQL Docker + les 3 apps natives en parallèle
# =============================================================================
.PHONY: dev-debug
dev-debug: ## [Dev] PostgreSQL Docker + Server (5201) + WebApp (5202) + AIServer (5203) en parallèle
	@for port in 5201 5202 5203; do \
	  if ss -tlnp "sport = :$$port" 2>/dev/null | grep -q LISTEN; then \
	    container=$$(docker ps --format '{{.Names}}\t{{.Ports}}' 2>/dev/null | awk -v p=":$$port" '$$0 ~ p {print $$1}'); \
	    if [ -n "$$container" ]; then \
	      echo "⚠️  Port $$port occupé par le conteneur Docker « $$container »."; \
	      echo "   → Arrêtez-le avec : docker stop $$container"; \
	    else \
	      echo "⚠️  Port $$port déjà utilisé par un autre processus."; \
	      echo "   → Cherchez avec : ss -tlnp 'sport = :$$port'"; \
	    fi; \
	    exit 1; \
	  fi; \
	done
	@echo "→ Démarrage de PostgreSQL..."
	docker compose -f $(DOCKER_DEBUG) up -d
	@echo "→ Build de la solution..."
	dotnet build -c Debug --no-restore
	@echo "→ Démarrage des apps (Ctrl+C pour tout arrêter)..."
	@trap 'kill 0' SIGINT; \
	LAMA_AI_LANGUAGE=fr LAMA_AI_MAX_CONCURRENT=3 \
	  dotnet run --project $(AISERVER_PROJECT) --no-build --urls http://127.0.0.1:5203 & \
	ASPNETCORE_ENVIRONMENT=Development LAMA_SERVER_ALLOW_SHUTDOWN=true LAMA_AI_SERVER_URL=http://127.0.0.1:5203 \
	  dotnet run --project $(SERVER_PROJECT) --no-build --urls http://127.0.0.1:5201 & \
	ASPNETCORE_ENVIRONMENT=Development LAMA_SERVER_URL=http://127.0.0.1:5201 LamaApi__BaseUrl=http://127.0.0.1:5201 \
	  dotnet run --project $(WEBAPP_PROJECT) --no-build --urls http://127.0.0.1:5202 & \
	wait

.PHONY: dev-debug-stop
dev-debug-stop: ## [Dev] Arrêter PostgreSQL Docker (les apps .NET s'arrêtent avec Ctrl+C)
	docker compose -f $(DOCKER_DEBUG) down

.PHONY: docker-stop-apps
docker-stop-apps: ## [Dev] Arrêter les conteneurs Docker qui pourraient bloquer les ports 5201/5202/5203
	@echo "→ Arrêt des conteneurs app Lama..."
	-docker stop lama-server lama-webapp lama-aiserver-fr lama-server-dev lama-webapp-dev 2>/dev/null
	@echo "✓ Ports 5201/5202/5203 libérés."

.PHONY: dev-debug-clean
dev-debug-clean: ## [Dev] Arrêter PostgreSQL et supprimer les volumes (réinitialiser DB)
	docker compose -f $(DOCKER_DEBUG) down -v

.PHONY: db-reset-sessions
db-reset-sessions: ## [Dev] Vider toutes les sessions en cours (DB + mémoire serveur, sans redémarrage)
	@echo "1/2 — Vidage des sessions en base de données..."
	@docker exec postgres-lama-debug psql -U lama_dev -d lama_dev -c " \
	  DELETE FROM sessions.turn_log; \
	  DELETE FROM sessions.players_in_game; \
	  DELETE FROM sessions.board_state; \
	  DELETE FROM sessions.games;" \
	  && echo "    ✓ DB vidée." \
	  || (echo "    ✗ Erreur DB — postgres-lama-debug est-il démarré ?" && exit 1)
	@echo "2/2 — Vidage de la mémoire du serveur..."
	@curl -sf -X POST http://127.0.0.1:5201/internal/games/clear \
	  && echo "    ✓ Mémoire serveur vidée." \
	  || echo "    ⚠ Serveur non joignable — redémarrez-le pour que la mémoire soit cohérente."
	@echo "✅ Réinitialisation terminée."

.PHONY: db-list-games
db-list-games: ## [Dev] Lister les sessions de jeu actives en base (avec ID, statut, date)
	@docker exec postgres-lama-debug psql -U lama_dev -d lama_dev -c \
	  "SELECT game_id, status, game_level, queue, created_at FROM sessions.games ORDER BY created_at DESC;" \
	  || echo "❌ Erreur — postgres-lama-debug est-il démarré ?"

.PHONY: db-delete-game
db-delete-game: ## [Dev] Supprimer une partie précise (usage : make db-delete-game GAME_ID=xxx)
	@test -n "$(GAME_ID)" || (echo "❌ Usage : make db-delete-game GAME_ID=<identifiant>" && exit 1)
	@echo "Suppression de la partie $(GAME_ID)..."
	@docker exec postgres-lama-debug psql -U lama_dev -d lama_dev -c " \
	  DELETE FROM sessions.turn_log        WHERE game_id = '$(GAME_ID)'; \
	  DELETE FROM sessions.players_in_game WHERE game_id = '$(GAME_ID)'; \
	  DELETE FROM sessions.board_state     WHERE game_id = '$(GAME_ID)'; \
	  DELETE FROM sessions.games           WHERE game_id = '$(GAME_ID)';" \
	  && echo "    ✓ DB : partie supprimée." \
	  || (echo "    ✗ Erreur DB." && exit 1)
	@curl -sf -X POST http://127.0.0.1:5201/internal/games/$(GAME_ID)/close \
	  && echo "    ✓ Mémoire serveur : partie clôturée." \
	  || echo "    ⚠ Serveur non joignable ou partie déjà absente de la mémoire."
	@echo "✅ Partie $(GAME_ID) supprimée."

.PHONY: health-debug
health-debug: ## [Dev] Vérifier les endpoints (Server 5201, WebApp 5202, AIServer 5203)
	@docker exec postgres-lama-debug pg_isready -U lama_dev -d lama_dev >/dev/null 2>&1 && echo "✓ PostgreSQL (5200) OK" || echo "✗ PostgreSQL (5200) KO"
	@curl -fsS http://localhost:5201/health && echo "✓ Server (5201) OK" || echo "✗ Server (5201) KO" || true
	@curl -fsS http://localhost:5202/ >/dev/null && echo "✓ WebApp (5202) OK" || echo "✗ WebApp (5202) KO" || true
	@curl -fsS http://localhost:5203/health && echo "✓ AIServer-fr (5203) OK" || echo "✗ AIServer-fr (5203) KO" || true

# =============================================================================
# Utilitaires
# =============================================================================
.PHONY: build
build: ## Compiler toute la solution
	dotnet build -c Release

.PHONY: test
test: ## Lancer tous les tests
	dotnet test -v minimal

.PHONY: test-watch
test-watch: ## Tests en mode watch (relance automatique)
	dotnet watch test --project tests/Lama.Console.UnitTests/Lama.Console.UnitTests.csproj

.PHONY: clean
clean: ## Nettoyer les artefacts de build
	dotnet clean
	rm -rf .deploy/stage

.PHONY: build-increment
build-increment: ## Incrémenter le numéro de build
	@bash tools/scripts/version/update-build-info.sh .build-info increment
	@bash tools/scripts/version/sync-to-csharp.sh .build-info

.PHONY: version-set
version-set: ## Fixer la version (usage : make version-set VERSION=1.2.3)
	@test -n "$(VERSION)" || (echo "❌ Usage : make version-set VERSION=1.2.3" && exit 1)
	@bash tools/scripts/version/update-build-info.sh .build-info set-version "$(VERSION)"
	@bash tools/scripts/version/sync-to-csharp.sh .build-info

.PHONY: build-generate
build-generate: ## Générer les infos de build (timestamp) — appelé automatiquement à chaque build
	@bash tools/scripts/version/update-build-info.sh .build-info generate
	@bash tools/scripts/version/sync-to-csharp.sh .build-info

.PHONY: docker-local-ps
docker-local-ps: ## État des conteneurs Docker locaux
	docker compose -f $(DOCKER_LOCAL) ps

.PHONY: health-local
health-local: ## Vérifier les endpoints locaux
	@curl -fsS http://localhost:5201/health && echo "✓ Server (5201) OK" || echo "✗ Server (5201) KO"

.PHONY: web-lobby-smoke
web-lobby-smoke: ## Smoke test Web lobby (register/create/start/my-games)
	bash tools/scripts/e2e/e2e-web-lobby-smoke.sh

# =============================================================================
# Aide
# =============================================================================
.PHONY: help
help: ## Afficher cette aide
	@grep -E '^[a-zA-Z_-]+:.*##' $(MAKEFILE_LIST) | \
	  awk 'BEGIN {FS = ":.*##"}; {printf "  \033[36m%-28s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help
