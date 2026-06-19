# =============================================================================
# LAMA — Makefile principal
# =============================================================================
# Usage : make <cible> [ARGS="..."] [SSH_KEY="~/.ssh/..."]
#
# 7 scénarios de démarrage / déploiement :
#   1. dev-local         Debug CLI en local (même PC, sans serveur)
#   2. dev-server        Serveur ASP.NET en mode développement (Rider ou terminal)
#   3. run-local         Exécution CLI locale simple (sans Rider)
#   4. docker-site-local Déploiement site web en local via Docker
#   5. docker-local      Déploiement + exécution stack complète en local (Docker)
#   6. deploy-site-prod  Déploiement site web sur VPS production
#   7. deploy-server-prod Déploiement serveur de jeu sur VPS production
# =============================================================================

SHELL := /bin/bash
ROOT_DIR := $(shell pwd)
CONSOLE_PROJECT := src/Console/Lama.Console/Lama.Console.csproj
SERVER_PROJECT  := src/Server/Lama.Server/Lama.Server.csproj
DOCKER_LOCAL    := tools/docker/docker-compose.local.yml

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
	bash tools/deployments/deploy-static-site.sh \
	  --mode prod \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY)

.PHONY: deploy-site-prod-dry
deploy-site-prod-dry: ## [Cas 6 - simulation] Dry-run du déploiement site VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie. Usage : make deploy-site-prod-dry SSH_KEY=~/.ssh/machines/playalama.key"; \
	  exit 1; \
	fi
	bash tools/deployments/deploy-static-site.sh \
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
	bash tools/deployments/deploy-vps.sh \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY)

.PHONY: deploy-server-prod-dry
deploy-server-prod-dry: ## [Cas 7 - simulation] Dry-run du déploiement serveur VPS
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "⚠  SSH_KEY non définie."; \
	  exit 1; \
	fi
	bash tools/deployments/deploy-vps.sh \
	  --target debian@playalama.online \
	  --ssh-key $(SSH_KEY) \
	  --dry-run

.PHONY: deploy-all-prod
deploy-all-prod: deploy-site-prod deploy-server-prod ## [Cas 6+7] Déploiement complet VPS (site + serveur)

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

.PHONY: health-prod
health-prod: ## Vérifier les endpoints de production
	@curl -fsS https://playalama.online/health && echo "✓ Prod OK" || echo "✗ Prod KO"

# =============================================================================
# Aide
# =============================================================================
.PHONY: help
help: ## Afficher cette aide
	@grep -E '^[a-zA-Z_-]+:.*##' $(MAKEFILE_LIST) | \
	  awk 'BEGIN {FS = ":.*##"}; {printf "  \033[36m%-28s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help

