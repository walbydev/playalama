# =============================================================================
# LAMA — Makefile unifié
# =============================================================================
# Convention : make <verbe> [ENV=<dev|staging|prod>] [ARGS="..."]
#
#   make help                → cette aide
#   make dev                 → démarrer en local (DB Docker + 3 apps natives)
#   make deploy ENV=prod     → déployer en production
#
# =============================================================================

SHELL := /bin/bash

# ─── CONFIG PROJET ────────────────────────────────────────────────────────────
DOTNET          ?= $(if $(wildcard $(HOME)/.dotnet/dotnet),$(HOME)/.dotnet/dotnet,dotnet)

CONSOLE_PROJECT := src/apps/Lama.Console/Lama.Console.csproj
SERVER_PROJECT  := src/apps/Lama.Server/Lama.Server.csproj
WEBAPP_PROJECT  := src/apps/Lama.WebApp/Lama.WebApp.csproj
AISERVER_PROJECT := src/apps/Lama.AIServer/Lama.AIServer.csproj

COMPOSE_LOCAL   := tools/docker/docker-compose.local.yml
COMPOSE_DEBUG   := tools/docker/docker-compose.local-debug.yml

# ─── ENDPOINTS LOCAUX ────────────────────────────────────────────────────────
SERVER_URL      := http://127.0.0.1:5201
WEBAPP_URL      := http://127.0.0.1:5202
AISERVER_URL    := http://127.0.0.1:5203

# ─── ENVIRONNEMENTS DISTANTS ──────────────────────────────────────────────────
ENV             ?= dev
DEPLOY_TARGET   ?= debian@playalama.online
SSH_KEY         ?= $(LAMA_DEPLOY_SSH_KEY)

HEALTH_PROD     ?= https://playalama.online/health
HEALTH_STAGING  ?= https://staging.playalama.online/health

# ─── BASE DE DONNÉES ──────────────────────────────────────────────────────────
DB_CONTAINER    := postgres-lama-debug
DB_NAME         := lama_dev
DB_USER         := lama_dev
DB_PASSWORD     := dev_password_change_me
DB_PORT         := 5200
DB_CONN_STRING  := Host=localhost;Port=$(DB_PORT);Database=$(DB_NAME);Username=$(DB_USER);Password=$(DB_PASSWORD);Ssl Mode=Disable

# ─── ADMIN ────────────────────────────────────────────────────────────────────
ADMIN_ENV       ?= $(ENV)
ADMIN_SERVER_URL ?=
ADMIN_SECRET    ?=
ADMIN_TOKEN     ?=

ADMIN_FLAGS = --env $(ADMIN_ENV) \
  $(if $(ADMIN_SERVER_URL),--server-url $(ADMIN_SERVER_URL),) \
  $(if $(ADMIN_SECRET),--admin-secret $(ADMIN_SECRET),) \
  $(if $(ADMIN_TOKEN),--token $(ADMIN_TOKEN),)

# ─── HELPERS ──────────────────────────────────────────────────────────────────
check-dotnet = @$(DOTNET) --version >/dev/null 2>&1 || { echo "❌ SDK .NET introuvable ($(DOTNET)). Astuce : make dotnet-info"; exit 155; }
VALID_ENV = $(filter $(ENV),dev staging prod)

# =============================================================================
# 1. DÉVELOPPEMENT
# =============================================================================

.PHONY: dev
dev: ## Démarrer la stack complète (DB Docker + Server 5201 + WebApp 5202 + AIServer 5203)
	$(check-dotnet)
	@for port in 5201 5202 5203; do \
	  if ss -tlnp "sport = :$$port" 2>/dev/null | grep -q LISTEN; then \
	    container=$$(docker ps --format '{{.Names}}\t{{.Ports}}' 2>/dev/null | awk -v p=":$$port" '$$0 ~ p {print $$1}'); \
	    if [ -n "$$container" ]; then \
	      echo "⚠️  Port $$port occupé par le conteneur « $$container » → make stop"; \
	    else \
	      echo "⚠️  Port $$port déjà utilisé → ss -tlnp 'sport = :$$port'"; \
	    fi; \
	    exit 1; \
	  fi; \
	done
	@echo "→ Démarrage de PostgreSQL..."
	docker compose -f $(COMPOSE_DEBUG) up -d
	@echo "→ Build de la solution..."
	$(DOTNET) build -c Debug --no-restore
	@echo "→ Démarrage des apps (Ctrl+C pour tout arrêter)..."
	@trap 'kill 0' SIGINT; \
	ASPNETCORE_ENVIRONMENT=Development LAMA_AI_LANGUAGE=fr LAMA_AI_MAX_CONCURRENT=3 \
	LAMA_LEXICON_CONNECTION_STRING="$(DB_CONN_STRING);Application Name=LamaAIServer.Dev;" \
	  $(DOTNET) run --project $(AISERVER_PROJECT) --no-build --urls http://127.0.0.1:5203 & \
	ASPNETCORE_ENVIRONMENT=Development LAMA_SERVER_ALLOW_SHUTDOWN=true LAMA_AI_SERVER_URL=http://127.0.0.1:5203 \
	  $(DOTNET) run --project $(SERVER_PROJECT) --no-build --urls http://127.0.0.1:5201 & \
	ASPNETCORE_ENVIRONMENT=Development LAMA_SERVER_URL=http://127.0.0.1:5201 LamaApi__BaseUrl=http://127.0.0.1:5201 \
	  $(DOTNET) run --project $(WEBAPP_PROJECT) --no-build --urls http://127.0.0.1:5202 & \
	wait

.PHONY: dev-server
dev-server: ## Lancer uniquement le serveur en mode Development (Rider/terminal)
	$(check-dotnet)
	ASPNETCORE_ENVIRONMENT=Development LAMA_SERVER_ALLOW_SHUTDOWN=true \
	  $(DOTNET) run --project $(SERVER_PROJECT) --urls http://127.0.0.1:5201

.PHONY: stop
stop: ## Arrêter les apps locales et les conteneurs Docker de dev
	-docker compose -f $(COMPOSE_DEBUG) down
	-docker stop lama-server lama-webapp lama-aiserver-fr lama-server-dev lama-webapp-dev postgres-lama-debug 2>/dev/null || true
	@echo "✓ Arrêté."

.PHONY: dev-clean
dev-clean: ## ⚠ Réinitialiser le dev (arrêter + supprimer les volumes DB)
	docker compose -f $(COMPOSE_DEBUG) down -v
	@echo "✓ Volumes supprimés. Relancer avec : make dev"

.PHONY: run
run: ## Exécuter le CLI (usage : make run ARGS="game create Alice")
	$(check-dotnet)
	LAMA_RUNTIME_MODE=local $(DOTNET) run --project $(CONSOLE_PROJECT) -- $(ARGS)

# =============================================================================
# 2. BUILD & TEST
# =============================================================================

.PHONY: build
build: ## Compiler toute la solution (Release)
	$(check-dotnet)
	$(DOTNET) build -c Release

.PHONY: test
test: ## Lancer tous les tests
	$(check-dotnet)
	$(DOTNET) test -v minimal

.PHONY: watch
watch: ## Tests en mode watch (relance automatique)
	$(check-dotnet)
	$(DOTNET) watch test --project tests/Lama.Console.UnitTests/Lama.Console.UnitTests.csproj

# =============================================================================
# 3. DÉPLOIEMENT
# =============================================================================

.PHONY: deploy
deploy: ## Déployer (usage : make deploy ENV=prod|staging [DRY=1])
	@if [ -z "$(VALID_ENV)" ] || [ "$(ENV)" = "dev" ]; then \
	  echo "❌ Usage : make deploy ENV=prod|staging [DRY=1]"; exit 1; fi
	@if [ -z "$(SSH_KEY)" ]; then \
	  echo "❌ SSH_KEY non définie. Usage : make deploy ENV=prod SSH_KEY=~/.ssh/playalama.key"; exit 1; fi
	bash tools/scripts/deploy/deploy-env.sh \
	  --env $(ENV) --target $(DEPLOY_TARGET) --ssh-key $(SSH_KEY) \
	  $(if $(DRY),--dry-run,)

.PHONY: status
status: ## État des containers distants (usage : make status ENV=prod|staging)
	@if [ -z "$(SSH_KEY)" ]; then echo "❌ SSH_KEY requise."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' && echo '' && docker images --format 'table {{.Repository}}\t{{.Tag}}\t{{.Size}}' | grep lama"

# =============================================================================
# 4. OBSERVABILITÉ
# =============================================================================

.PHONY: logs
logs: ## Suivre les logs (usage : make logs [ENV=prod|staging])
ifeq ($(ENV),dev)
	@docker compose -f $(COMPOSE_DEBUG) logs -f --tail=100
else
	@if [ -z "$(SSH_KEY)" ]; then echo "❌ SSH_KEY requise pour ENV=$(ENV)."; exit 1; fi
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /opt/playalama/$(ENV) && docker compose -p $(ENV) logs -f --tail=100 lama-server-$(ENV) lama-webapp-$(ENV)"
endif

.PHONY: health
health: ## Health check (usage : make health [ENV=dev|prod|staging])
ifeq ($(ENV),dev)
	@docker exec $(DB_CONTAINER) pg_isready -U $(DB_USER) -d $(DB_NAME) >/dev/null 2>&1 && echo "✓ PostgreSQL ($(DB_PORT)) OK" || echo "✗ PostgreSQL ($(DB_PORT)) KO"
	@curl -fsS $(SERVER_URL)/health && echo " ✓ Server OK" || echo "✗ Server KO"
	@curl -fsS $(WEBAPP_URL)/ >/dev/null && echo "✓ WebApp OK" || echo "✗ WebApp KO"
	@curl -fsS $(AISERVER_URL)/health && echo "✓ AIServer OK" || echo "✗ AIServer KO"
else ifeq ($(ENV),prod)
	@curl -fsS $(HEALTH_PROD) && echo " ✓ Prod OK" || echo "✗ Prod KO"
else ifeq ($(ENV),staging)
	@curl -fsS $(HEALTH_STAGING) && echo " ✓ Staging OK" || echo "✗ Staging KO"
endif

# =============================================================================
# 5. BASE DE DONNÉES
# =============================================================================

.PHONY: db
db: ## Shell psql interactif (dev local)
	@docker exec -it $(DB_CONTAINER) psql -U $(DB_USER) -d $(DB_NAME)

.PHONY: db-reset
db-reset: ## ⚠ Vider les sessions de jeu (usage : make db-reset [ENV=dev])
	@if [ "$(ENV)" != "dev" ] && [ "$(FORCE)" != "1" ]; then \
	  echo "❌ db-reset réservé au dev. Pour $(ENV) : make db-reset ENV=$(ENV) FORCE=1"; exit 1; fi
	@echo "→ Vidage des sessions en base..."
	@docker exec $(DB_CONTAINER) psql -U $(DB_USER) -d $(DB_NAME) -c " \
	  DELETE FROM sessions.turn_log; \
	  DELETE FROM sessions.players_in_game; \
	  DELETE FROM sessions.board_state; \
	  DELETE FROM sessions.games;" \
	  && echo "    ✓ DB vidée." \
	  || (echo "    ✗ Erreur DB — le conteneur tourne-t-il ? (make dev)" && exit 1)
	@echo "→ Vidage de la mémoire serveur..."
	@curl -sf -X POST http://127.0.0.1:5201/internal/games/clear \
	  && echo "    ✓ Mémoire serveur vidée." \
	  || echo "    ⚠ Serveur non joignable — redémarrez-le pour cohérence."
	@echo "✅ Réinitialisation terminée."

.PHONY: db-query
db-query: ## Requête SQL libre (usage : make db-query ARGS="SELECT * FROM sessions.games")
	docker exec $(DB_CONTAINER) psql -U $(DB_USER) -d $(DB_NAME) -c "$(ARGS)"

.PHONY: db-delete-game
db-delete-game: ## Supprimer une partie (usage : make db-delete-game GAME_ID=xxx)
	@test -n "$(GAME_ID)" || (echo "❌ Usage : make db-delete-game GAME_ID=<id>" && exit 1)
	@echo "Suppression de la partie $(GAME_ID)..."
	@docker exec $(DB_CONTAINER) psql -U $(DB_USER) -d $(DB_NAME) -c " \
	  DELETE FROM sessions.turn_log        WHERE game_id = '$(GAME_ID)'; \
	  DELETE FROM sessions.players_in_game WHERE game_id = '$(GAME_ID)'; \
	  DELETE FROM sessions.board_state     WHERE game_id = '$(GAME_ID)'; \
	  DELETE FROM sessions.games           WHERE game_id = '$(GAME_ID)';" \
	  && echo "    ✓ DB : partie supprimée." \
	  || (echo "    ✗ Erreur DB." && exit 1)
	@curl -sf -X POST http://127.0.0.1:5201/internal/games/$(GAME_ID)/close \
	  && echo "    ✓ Mémoire serveur : partie clôturée." \
	  || echo "    ⚠ Serveur non joignable ou partie déjà absente."
	@echo "✅ Partie $(GAME_ID) supprimée."

# =============================================================================
# 6. INFRASTRUCTURE VPS
# =============================================================================

.PHONY: infra-init
infra-init: ## Initialiser le VPS (1 seule fois, idempotent) [DRY=1]
	@if [ -z "$(SSH_KEY)" ]; then echo "❌ SSH_KEY requise."; exit 1; fi
	bash tools/scripts/deploy/setup-vps.sh \
	  --target $(DEPLOY_TARGET) --ssh-key $(SSH_KEY) \
	  $(if $(DRY),--dry-run,)

.PHONY: infra-proxy
infra-proxy: ## (Re)déployer Traefik sur le VPS
	@if [ -z "$(SSH_KEY)" ]; then echo "❌ SSH_KEY requise."; exit 1; fi
	scp -i $(SSH_KEY) tools/docker/traefik.yml                $(DEPLOY_TARGET):/opt/playalama/traefik/traefik.yml
	scp -i $(SSH_KEY) tools/docker/docker-compose.traefik.yml  $(DEPLOY_TARGET):/opt/playalama/traefik/docker-compose.yml
	scp -i $(SSH_KEY) tools/docker/nginx-docker-api-proxy.conf $(DEPLOY_TARGET):/opt/playalama/traefik/nginx-docker-api-proxy.conf
	ssh -i $(SSH_KEY) $(DEPLOY_TARGET) \
	  "cd /opt/playalama/traefik && docker compose pull && docker compose up -d"

.PHONY: infra-prune
infra-prune: ## Nettoyer les anciens containers/images sur le VPS [DRY=1]
	@if [ -z "$(SSH_KEY)" ]; then echo "❌ SSH_KEY requise."; exit 1; fi
	bash tools/scripts/deploy/cleanup-vps.sh \
	  --ssh-key $(SSH_KEY) --target $(DEPLOY_TARGET) \
	  $(if $(DRY),--dry-run,)

.PHONY: infra-destroy
infra-destroy: ## ⚠ DESTRUCTIF — Remise à zéro complète du VPS [FORCE=1] [INCLUDE_TRAEFIK=1]
	@if [ -z "$(SSH_KEY)" ]; then echo "❌ SSH_KEY requise."; exit 1; fi
	@echo "⚠ Cette action est IRRÉVERSIBLE. Confirmer avec FORCE=1"
	@test "$(FORCE)" = "1" || (echo "❌ Ajouter FORCE=1 pour confirmer."; exit 1)
	bash tools/scripts/deploy/clean-all-vps.sh \
	  --target $(DEPLOY_TARGET) --ssh-key $(SSH_KEY) \
	  $(if $(INCLUDE_TRAEFIK),--include-traefik,)

# =============================================================================
# 7. VERSIONNING
# =============================================================================

.PHONY: release
release: ## Gérer la version (VERSION=x.y.z | BUILD=increment)
ifeq ($(BUILD),increment)
	@bash tools/scripts/version/update-build-info.sh .build-info increment
else ifneq ($(VERSION),)
	@bash tools/scripts/version/update-build-info.sh .build-info set-version "$(VERSION)"
else
	@echo "❌ Usage : make release VERSION=1.2.3  ou  make release BUILD=increment"; exit 1
endif
	@bash tools/scripts/version/sync-to-csharp.sh .build-info

.PHONY: build-generate
build-generate: ## Régénérer les infos de build (appelé automatiquement par le build)
	@bash tools/scripts/version/update-build-info.sh .build-info generate
	@bash tools/scripts/version/sync-to-csharp.sh .build-info

# =============================================================================
# 8. STACK DOCKER LOCALE
# =============================================================================

.PHONY: docker-up
docker-up: ## Démarrer la stack Docker locale complète (server + webapp + nginx)
	docker compose -f $(COMPOSE_LOCAL) up -d --build

.PHONY: docker-down
docker-down: ## Arrêter la stack Docker locale
	docker compose -f $(COMPOSE_LOCAL) down

.PHONY: docker-rebuild
docker-rebuild: ## Rebuild + redémarrage forcé de la stack Docker locale
	docker compose -f $(COMPOSE_LOCAL) up -d --build --force-recreate

.PHONY: docker-logs
docker-logs: ## Suivre les logs de la stack Docker locale
	docker compose -f $(COMPOSE_LOCAL) logs -f

.PHONY: docker-ps
docker-ps: ## État des conteneurs Docker locaux
	docker compose -f $(COMPOSE_LOCAL) ps

# =============================================================================
# 9. LAMA-SPECIFIQUE — Tests E2E & données
# =============================================================================

.PHONY: smoke
smoke: ## Smoke test E2E en ligne (CLI)
	bash tools/scripts/e2e/e2e-online-smoke.sh

.PHONY: smoke-web
smoke-web: ## Smoke test Web lobby (register/create/start/my-games)
	bash tools/scripts/e2e/e2e-web-lobby-smoke.sh

.PHONY: lexicon-import
lexicon-import: ## Importer le lexique JSONL dans PostgreSQL (usage : make lexicon-import ARGS="...")
	bash tools/scripts/db/import-lexicon.sh $(ARGS)

.PHONY: lexicon-import-all
lexicon-import-all: ## Importer le lexique sur dev→staging→prod
	bash tools/scripts/db/import-lexicon-all.sh $(ARGS)

# =============================================================================
# 10. ADMINISTRATION
# =============================================================================

.PHONY: admin-env
admin-env: ## Afficher l'environnement admin résolu (dev/staging/prod)
	@bash tools/scripts/admin/admin-env.sh $(ADMIN_FLAGS) $(ARGS)

.PHONY: admin-games
admin-games: ## Gérer les parties (usage: make admin-games ENV=dev ARGS="list")
	@bash tools/scripts/admin/admin-games.sh $(ADMIN_FLAGS) $(ARGS)

.PHONY: admin-users
admin-users: ## Gérer les joueurs (usage: make admin-users ARGS="register --username u --password p")
	@bash tools/scripts/admin/admin-users.sh $(ADMIN_FLAGS) $(ARGS)

.PHONY: admin-reset
admin-reset: ## Remises à zéro (usage: make admin-reset ARGS="reset-games --yes --json")
	@bash tools/scripts/admin/admin-reset.sh $(ADMIN_FLAGS) $(ARGS)

.PHONY: admin-reset-games
admin-reset-games: ## Reset parties (dev: purge DB+mémoire, stg/prod: terminate-all)
	@$(MAKE) --no-print-directory admin-reset ARGS="reset-games --yes $(ARGS)"

.PHONY: admin-reset-users
admin-reset-users: ## Reset joueurs (dev uniquement)
	@$(MAKE) --no-print-directory admin-reset ARGS="reset-users --yes $(ARGS)"

.PHONY: admin-reset-stats
admin-reset-stats: ## Reset stats (dev uniquement)
	@$(MAKE) --no-print-directory admin-reset ARGS="reset-stats --yes $(ARGS)"

.PHONY: admin-reset-all
admin-reset-all: ## Reset complet jeux+stats+joueurs (dev uniquement)
	@$(MAKE) --no-print-directory admin-reset ARGS="reset-all --yes $(ARGS)"

.PHONY: admin-ensure-root
admin-ensure-root: ## Garantir l'accès root/root (dev uniquement)
	@$(MAKE) --no-print-directory admin-reset ARGS="ensure-root --yes $(ARGS)"

# =============================================================================
# 11. UTILITAIRES
# =============================================================================

.PHONY: dotnet-info
dotnet-info: ## Afficher le SDK .NET utilisé par le Makefile
	@echo "DOTNET utilisé : $(DOTNET)"
	@$(DOTNET) --version || true
	@$(DOTNET) --list-sdks || true

.PHONY: clean
clean: ## Nettoyer les artefacts de build
	$(check-dotnet)
	$(DOTNET) clean
	rm -rf .deploy/stage

# =============================================================================
# Aide
# =============================================================================
.PHONY: help
help: ## Afficher cette aide
	@printf "\n  \033[1mLAMA — Commandes unifiées\033[0m\n"
	@printf "  Convention : make <verbe> [ENV=dev|staging|prod] [ARGS=\"...\"]\n\n"
	@grep -E '^[a-zA-Z_-]+:.*##' $(MAKEFILE_LIST) | \
	  awk 'BEGIN {FS = ":.*##"}; {printf "  \033[36m%-18s\033[0m %s\n", $$1, $$2}' \
	  | sort
	@echo ""

.DEFAULT_GOAL := help
