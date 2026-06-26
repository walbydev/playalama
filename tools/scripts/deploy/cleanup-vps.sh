#!/usr/bin/env bash
# =============================================================================
# cleanup-vps.sh — Nettoyage VPS post-migration vers image-only
# =============================================================================
# Supprime les anciens containers portal/game-webapp, les images orphelines,
# et propose de migrer les données de /srv/playalama vers /opt/playalama.
#
# Usage:
#   bash tools/scripts/deploy/cleanup-vps.sh --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/deploy/cleanup-vps.sh --ssh-key ~/.ssh/playalama.key --dry-run
# =============================================================================
set -euo pipefail

SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
DRY_RUN=false

log()  { printf '\033[0;36m[CLEANUP]\033[0m %s\n' "$*"; }
ok()   { printf '\033[0;32m[CLEANUP][OK]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[CLEANUP][WARN]\033[0m %s\n' "$*"; }
err()  { printf '\033[0;31m[CLEANUP][ERR]\033[0m %s\n' "$*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --ssh-key) SSH_KEY_FILE="$2"; shift 2 ;;
    --target)  REMOTE_TARGET="$2"; shift 2 ;;
    --dry-run) DRY_RUN=true; shift ;;
    *) err "Option inconnue: $1" ;;
  esac
done

SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

CLEANUP_SCRIPT='
set -euo pipefail

echo ""
echo "=== Containers actifs ==="
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo ""
echo "=== Arrêt stacks depuis ancien répertoire /srv/playalama ==="
for env in prod staging; do
  OLD_DIR="/srv/playalama/$env"
  if [ -d "$OLD_DIR" ]; then
    if [ -f "$OLD_DIR/docker-compose.yml" ] || [ -f "$OLD_DIR/tools/docker/docker-compose.$env.yml" ]; then
      echo "  Arrêt stack $env depuis $OLD_DIR..."
      cd "$OLD_DIR" && docker compose -p "$env" down --remove-orphans 2>/dev/null || true
    fi
  fi
done

echo ""
echo "=== Nettoyage images dangling ==="
docker image prune -f

echo ""
echo "=== Containers actifs après nettoyage ==="
docker ps --format "table {{.Names}}\t{{.Status}}"

echo ""
echo "✓ Nettoyage VPS terminé"
'

if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "bash -s" <<SCRIPT\n%s\nSCRIPT\n' "$REMOTE_TARGET" "$CLEANUP_SCRIPT"
  exit 0
fi

log "Connexion à $REMOTE_TARGET..."
ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$CLEANUP_SCRIPT"
ok "Nettoyage terminé"
