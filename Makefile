# =============================================================================
# LAMA — Makefile principal
# =============================================================================
# Usage : make <cible> [ARGS="..."] [SSH_KEY="~/.ssh/..."]
#
# Déploiement VPS (PROD + STAGING avec Traefik) :
#   setup-vps        Initialisation VPS (1 seule fois)
#   deploy-traefik   (Re)déployer Traefik sur le VPS
#   deploy-prod      Déployer l'environnement PROD  (playalama.online)
#   deploy-staging   Déployer l'environnement STAGING (staging.playalama.online)
#
# 7 scénarios de démarrage local / CI :
#   1. dev-local         Debug CLI en local (même PC, sans serveur)
#   2. dev-server        Serveur ASP.NET en mode développement (Rider ou terminal)
#   3. run-local         Exécution CLI locale simple (sans Rider)
#   4. docker-site-local Déploiement site web en local via Docker
#   5. docker-local      Déploiement + exécution stack complète en local (Docker)
#   6. deploy-site-prod  Déploiement site web sur VPS production (legacy)
#   7. deploy-server-prod Déploiement serveur de jeu sur VPS production (legacy)
# =============================================================================

SHELL := /bin/bash
ROOT_DIR := $(shell pwd)
CONSOLE_PROJECT := src/apps/Lama.Console/Lama.Console.csproj
SERVER_PROJECT  := src/apps/Lama.Server/Lama.Server.csproj
WEBAPP_PROJECT  := src/apps/Lama.GameWebApp/Lama.GameWebApp.csproj
DOCKER_LOCAL    := tools/docker/docker-compose.local.yml
DOCKER_OPTION_A := tools/docker/docker-compose.local-debug.yml

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
	dotnet run --project $(SERVER_PROJECT) --urls http://127.0.0.1:5000

# =============================================================================
# 3. Exécution locale CLI (sans Rider)
# =============================================================================
.PHONY: run-local
run-local: ## [Cas 3] Exécuter une commande CLI locale  →  make run-local ARGS="game create Alice"
	LAMA_RUNTIME_MODE=local \
	dotnet run --project $(CONSOLE_PROJECT) -- $(ARGS)

# =============================================================================
# 4. Site web local (Docker nginx uniquement)
# =============================================================================
.PHONY: docker-site-local
docker-site-local: ## [Cas 4] Démarrer nginx local seul (site statique sur http://localhost)
	docker compose -f $(DOCKER_LOCAL) up -d nginx

.PHONY: docker-site-local-stop
docker-site-local-stop: ## Arrêter nginx local
	docker compose -f $(DOCKER_LOCAL) stop nginx

# =============================================================================
# 5. Stack Docker locale complète (serveur + nginx)
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
# 6. Déploiement site web VPS production
# =============================================================================
.PHONY: deploy-site-prod
deploy-site-prod: ## [Cas 6] Déployer site statique sur VPS  →  make deploy-site-prod SSH_KEY=~/.ssh/...
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-site-prod SSH_KEY=~/.ssh/machines/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/deploy-static-site.sh \
	  --mode prod \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY)

.PHONY: deploy-site-prod-dry
deploy-site-prod-dry: ## [Cas 6 - simulation] Dry-run du déploiement site VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-site-prod-dry SSH_KEY=~/.ssh/machines/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/deploy-static-site.sh \
	  --mode prod \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

# =============================================================================
# 7. Déploiement serveur de jeu VPS production
# =============================================================================
.PHONY: deploy-server-prod
deploy-server-prod: ## [Cas 7] Build + publish + déployer Lama.Server sur VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-server-prod SSH_KEY=~/.ssh/machines/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/deploy-vps.sh \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY)

.PHONY: deploy-server-prod-dry
deploy-server-prod-dry: ## [Cas 7 - simulation] Dry-run du déploiement serveur VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie."; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/deploy-vps.sh \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

.PHONY: deploy-all-prod
deploy-all-prod: deploy-site-prod deploy-server-prod ## [Cas 6+7] Déploiement complet VPS (site + serveur) — LEGACY

# =============================================================================
# VPS PROD + STAGING — Déploiement Dockerisé avec Traefik
# =============================================================================
DEPLOY_TARGET ?= debian@playalama.online

.PHONY: setup-vps
setup-vps: ## [VPS] Initialisation du VPS (1 seule fois) — installe Docker, crée la structure, clone le repo, lance Traefik
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

# BUNDLE_FILE peut être surchargé : make push-bundle BUNDLE_FILE=/tmp/mabranch.bundle
BUNDLE_FILE ?= /tmp/playalama-$(shell date +%Y%m%d%H%M%S).bundle
VPS_BARE_REPO ?= /opt/playalama/git/playalama.git

.PHONY: push-bundle
push-bundle: ## [VPS] Envoyer les commits locaux vers le bare repo du VPS (remplace git push)
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make push-bundle SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	@echo "→ Création du bundle git..."
	git bundle create $(BUNDLE_FILE) --branches --tags
	@echo "→ Copie vers le VPS..."
	scp -i $(SSH_KEY) $(BUNDLE_FILE) $(DEPLOY_TARGET):/tmp/playalama.bundle
	@echo "→ Mise à jour du bare repo VPS..."
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "git --git-dir=$(VPS_BARE_REPO) fetch /tmp/playalama.bundle 'refs/heads/*:refs/heads/*' && rm /tmp/playalama.bundle"
	rm -f $(BUNDLE_FILE)
	@echo "✓ Bundle poussé — lancer make deploy-prod / make deploy-staging"

.PHONY: setup-vps-bundle
setup-vps-bundle: ## [VPS] Setup VPS complet via bundle SSH (quand Gitea LAN est inaccessible depuis le VPS)
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make setup-vps-bundle SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	bash tools/scripts/deploy/setup-vps.sh \
	  --target $(DEPLOY_TARGET) \
	  --ssh-key $(SSH_KEY) \
	  --bundle $(ROOT_DIR)

.PHONY: deploy-traefik
deploy-traefik: ## [VPS] (Re)déployer Traefik sur le VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-traefik SSH_KEY=~/.ssh/playalama.key"; \
	  exit 1; \
	fi
	scp -i $(SSH_KEY) tools/docker/traefik.yml               $(DEPLOY_TARGET):/opt/playalama/traefik/traefik.yml
	scp -i $(SSH_KEY) tools/docker/docker-compose.traefik.yml $(DEPLOY_TARGET):/opt/playalama/traefik/docker-compose.yml
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
logs-prod: ## Afficher les logs PROD en temps réel (lama-server-prod)
	@if [ -z "$(SSH_KEY)" ]; then echo "⚠  SSH_KEY non définie."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /srv/playalama/prod && docker compose -f tools/docker/docker-compose.prod.yml logs -f --tail=100"

.PHONY: logs-staging
logs-staging: ## Afficher les logs STAGING en temps réel (lama-server-staging)
	@if [ -z "$(SSH_KEY)" ]; then echo "⚠  SSH_KEY non définie."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /srv/playalama/staging && docker compose -f tools/docker/docker-compose.staging.yml logs -f --tail=100"

# =============================================================================
# OPTION A: Debug Natif + PostgreSQL Docker (Recommandé pour développement)
# =============================================================================
.PHONY: option-a-start
option-a-start: ## [OPTION A] Démarrer PostgreSQL en Docker (ports 5200/5201/5202)
	bash tools/scripts/dev/start-local-debug-option-a.sh

.PHONY: option-a-server
option-a-server: ## [OPTION A] Lancer Lama.Server natif sur port 5201
	dotnet run --project $(SERVER_PROJECT)

.PHONY: option-a-webapp
option-a-webapp: ## [OPTION A] Lancer Lama.GameWebApp natif sur port 5202
	dotnet run --project $(WEBAPP_PROJECT)

.PHONY: option-a-stop
option-a-stop: ## [OPTION A] Arrêter PostgreSQL Docker
	docker compose -f $(DOCKER_OPTION_A) down

.PHONY: option-a-clean
option-a-clean: ## [OPTION A] Arrêter et supprimer les volumes (réinitialiser DB)
	docker compose -f $(DOCKER_OPTION_A) down -v

.PHONY: option-a-logs
option-a-logs: ## [OPTION A] Suivre les logs PostgreSQL
	docker compose -f $(DOCKER_OPTION_A) logs -f postgres-lama

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

.PHONY: docker-local-ps
docker-local-ps: ## État des conteneurs Docker locaux
	docker compose -f $(DOCKER_LOCAL) ps

.PHONY: health-local
health-local: ## Vérifier les endpoints locaux
	@curl -fsS http://localhost:5000/health && echo "✓ Server OK" || echo "✗ Server KO"
	@curl -fsS http://localhost/health && echo "✓ nginx→Server OK" || echo "✗ nginx→Server KO"

.PHONY: health-option-a
health-option-a: ## [OPTION A] Vérifier les endpoints (PostgreSQL 5200, Server 5201, WebApp 5202)
	@docker exec postgres-lama-option-a pg_isready -U lama_dev -d lama_dev >/dev/null 2>&1 && echo "✓ PostgreSQL (5200) OK" || echo "✗ PostgreSQL (5200) KO"
	@curl -fsS http://localhost:5201/health && echo "✓ Server (5201) OK" || echo "✗ Server (5201) KO" || true
	@curl -fsS http://localhost:5202/ && echo "✓ WebApp (5202) OK" || echo "✗ WebApp (5202) KO" || true

.PHONY: web-lobby-smoke
web-lobby-smoke: ## [OPTION A] Smoke test Web lobby (register/create/start/my-games)
	bash tools/scripts/e2e/e2e-web-lobby-smoke.sh

# =============================================================================
# Aide
# =============================================================================
.PHONY: help
help: ## Afficher cette aide
	@grep -E '^[a-zA-Z_-]+:.*##' $(MAKEFILE_LIST) | \
	  awk 'BEGIN {FS = ":.*##"}; {printf "  \033[36m%-28s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help

