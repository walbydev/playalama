#!/usr/bin/env bash
# docker-local.sh - Gestion du stack Docker local LAMA
# =====================================================
# Démarre / arrête / rebuild le stack Docker local (nginx + lama-server).
# Pour le debug Rider (Cas 2), démarre nginx seul (--nginx-only) et lance
# Lama.Server directement depuis Rider avec les env vars adéquates.
#
# Usage :
#   tools/scripts/docker-local.sh [up|down|rebuild|logs|status|nginx-only]
#
# Exemples :
#   tools/scripts/docker-local.sh up            # Stack complète
#   tools/scripts/docker-local.sh nginx-only    # Nginx seul (debug Rider)
#   tools/scripts/docker-local.sh logs          # Suivre les logs
#   tools/scripts/docker-local.sh down          # Tout arrêter

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/tools/docker/docker-compose.local.yml"

CMD="${1:-up}"

# Couleurs
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
RESET='\033[0m'

info()  { printf "${CYAN}[docker-local]${RESET} %s\n" "$*"; }
ok()    { printf "${GREEN}✓${RESET} %s\n" "$*"; }
warn()  { printf "${YELLOW}⚠${RESET} %s\n" "$*" >&2; }

require_cmd() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "❌ Commande manquante : $1" >&2; exit 1
    fi
}

require_cmd docker

cd "$ROOT_DIR"

case "$CMD" in
  up)
    info "Démarrage stack complète (lama-server + nginx)..."
    docker compose -f "$COMPOSE_FILE" up -d --build
    ok "Stack démarrée"
    echo ""
    echo "  Site web    : http://localhost/"
    echo "  Server API  : http://localhost:5000/health"
    echo "  Download    : http://localhost/download/"
    ;;

  nginx-only)
    info "Démarrage nginx seul (mode debug Rider — serveur géré par Rider)"
    docker compose -f "$COMPOSE_FILE" up -d nginx
    ok "nginx démarré sur http://localhost"
    echo ""
    warn "Démarrez Lama.Server dans Rider avec :"
    echo "  ASPNETCORE_ENVIRONMENT=Development"
    echo "  ASPNETCORE_URLS=http://127.0.0.1:5000"
    echo "  LAMA_SERVER_ALLOW_SHUTDOWN=true"
    ;;

  rebuild)
    info "Rebuild + redémarrage forcé..."
    docker compose -f "$COMPOSE_FILE" up -d --build --force-recreate
    ok "Stack reconstruite et démarrée"
    ;;

  down)
    info "Arrêt de la stack..."
    docker compose -f "$COMPOSE_FILE" down
    ok "Stack arrêtée"
    ;;

  logs)
    info "Logs en temps réel (Ctrl+C pour quitter)..."
    docker compose -f "$COMPOSE_FILE" logs -f
    ;;

  status|ps)
    docker compose -f "$COMPOSE_FILE" ps
    ;;

  health)
    info "Vérification des endpoints..."
    curl -fsS http://localhost:5000/health >/dev/null && ok "lama-server : http://localhost:5000/health" || warn "lama-server non disponible"
    curl -fsS http://localhost/ >/dev/null          && ok "nginx       : http://localhost/"            || warn "nginx non disponible"
    ;;

  *)
    echo "Usage : $0 [up|down|rebuild|logs|status|nginx-only|health]"
    exit 1
    ;;
esac

