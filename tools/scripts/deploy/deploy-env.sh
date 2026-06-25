#!/usr/bin/env bash
# =============================================================================
# deploy-env.sh — Déploiement image-only d'un environnement sur le VPS
# =============================================================================
# Stratégie : build local → docker save | gzip → scp → docker load → up
# Le VPS ne compile RIEN et n'a plus besoin du code source .NET.
#
# Usage:
#   bash tools/scripts/deploy/deploy-env.sh --env prod   --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/deploy/deploy-env.sh --env staging --ssh-key ~/.ssh/playalama.key
#
# Options:
#   --env <prod|staging>        Environnement cible (obligatoire)
#   --ssh-key <file>            Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --tag <value>               Tag des images (défaut: timestamp)
#   --target <user@host>        Cible SSH (défaut: debian@playalama.online)
#   --skip-healthcheck          Ne pas vérifier l'état après déploiement
#   --skip-cleanup              Ne pas supprimer les anciennes images/containers sur le VPS
#   --dry-run                   Afficher les commandes sans les exécuter
#   -h, --help                  Afficher cette aide
#
# Variables d'environnement :
#   LAMA_DEPLOY_SSH_KEY         Chemin vers la clé SSH
#   LAMA_DEPLOY_TARGET          Cible SSH (user@host)
# =============================================================================
set -euo pipefail

DEPLOY_ENV=""
SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
DEPLOY_TAG="${LAMA_DEPLOY_TAG:-$(date +%Y%m%d-%H%M%S)}"
SKIP_HEALTHCHECK=false
SKIP_CLEANUP=false
DRY_RUN=false

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

usage() {
  grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \?//'
  exit 0
}

log()  { printf '\033[0;36m[DEPLOY]\033[0m %s\n' "$*"; }
err()  { printf '\033[0;31m[DEPLOY][ERREUR]\033[0m %s\n' "$*" >&2; exit 1; }
ok()   { printf '\033[0;32m[DEPLOY][OK]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[DEPLOY][WARN]\033[0m %s\n' "$*"; }

run_remote() {
  local cmd="$1"
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "%s"\n' "$REMOTE_TARGET" "$cmd"
    return 0
  fi
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "$cmd"
}

# Parsing des arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)             DEPLOY_ENV="$2"; shift 2 ;;
    --ssh-key)         SSH_KEY_FILE="$2"; shift 2 ;;
    --target)          REMOTE_TARGET="$2"; shift 2 ;;
    --tag)             DEPLOY_TAG="$2"; shift 2 ;;
    --skip-healthcheck) SKIP_HEALTHCHECK=true; shift ;;
    --skip-cleanup)    SKIP_CLEANUP=true; shift ;;
    --dry-run)         DRY_RUN=true; shift ;;
    -h|--help)         usage ;;
    *) err "Option inconnue: $1" ;;
  esac
done

# Validation
[[ -z "$DEPLOY_ENV" ]] && err "--env obligatoire (prod ou staging)"
[[ "$DEPLOY_ENV" != "prod" && "$DEPLOY_ENV" != "staging" ]] && err "--env doit être 'prod' ou 'staging'"

# Configuration selon l'environnement
case "$DEPLOY_ENV" in
  prod)
    REMOTE_DIR="/opt/playalama/prod"
    COMPOSE_FILE="tools/docker/docker-compose.prod.yml"
    HEALTH_URL="https://playalama.online/health"
    ;;
  staging)
    REMOTE_DIR="/opt/playalama/staging"
    COMPOSE_FILE="tools/docker/docker-compose.staging.yml"
    HEALTH_URL="https://staging.playalama.online/health"
    ;;
esac

# Arguments SSH
SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

SERVER_IMAGE="lama-server:$DEPLOY_TAG"
WEBAPP_IMAGE="lama-webapp:$DEPLOY_TAG"
BUNDLE_FILE="/tmp/lama-images-$DEPLOY_TAG.tar.gz"

log "════════════════════════════════════════════════════"
log " Déploiement LAMA (image-only) — $(echo "$DEPLOY_ENV" | tr '[:lower:]' '[:upper:]')"
log " Cible SSH : $REMOTE_TARGET"
log " Répertoire VPS : $REMOTE_DIR"
log " Tag images : $DEPLOY_TAG"
log "════════════════════════════════════════════════════"

# ── 1. Build des images localement ─────────────────────────────────────────
log "Build image serveur ($SERVER_IMAGE)..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m docker build -f tools/docker/Dockerfile.server -t %s .\n' "$SERVER_IMAGE"
else
  docker build -f tools/docker/Dockerfile.server -t "$SERVER_IMAGE" "$ROOT_DIR"
fi

log "Build image webapp ($WEBAPP_IMAGE)..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m docker build -f tools/docker/Dockerfile.webapp -t %s .\n' "$WEBAPP_IMAGE"
else
  docker build -f tools/docker/Dockerfile.webapp -t "$WEBAPP_IMAGE" "$ROOT_DIR"
fi

# ── 2. Empaquetage des images ───────────────────────────────────────────────
log "Empaquetage des images → $BUNDLE_FILE..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m docker save %s %s | gzip > %s\n' "$SERVER_IMAGE" "$WEBAPP_IMAGE" "$BUNDLE_FILE"
else
  docker save "$SERVER_IMAGE" "$WEBAPP_IMAGE" | gzip > "$BUNDLE_FILE"
  BUNDLE_SIZE=$(du -sh "$BUNDLE_FILE" | cut -f1)
  log "Bundle créé : $BUNDLE_FILE ($BUNDLE_SIZE)"
fi

# ── 3. Synchronisation des fichiers de config sur le VPS ───────────────────
log "Préparation du répertoire distant..."
run_remote "mkdir -p '$REMOTE_DIR/assets/languages' '$REMOTE_DIR/artifacts/zip' '$REMOTE_DIR/tools/docker'"

log "Sync compose file..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m scp %s %s:%s/docker-compose.yml\n' "$COMPOSE_FILE" "$REMOTE_TARGET" "$REMOTE_DIR"
  printf '\033[0;33m[DRY-RUN]\033[0m scp tools/docker/nginx-downloads.conf %s:%s/tools/docker/\n' "$REMOTE_TARGET" "$REMOTE_DIR"
else
  scp "${SSH_ARGS[@]}" "$ROOT_DIR/$COMPOSE_FILE" "$REMOTE_TARGET:$REMOTE_DIR/docker-compose.yml"
  scp "${SSH_ARGS[@]}" "$ROOT_DIR/tools/docker/nginx-downloads.conf" "$REMOTE_TARGET:$REMOTE_DIR/tools/docker/"
fi

log "Sync assets/languages..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m rsync -az assets/languages/ %s:%s/assets/languages/\n' "$REMOTE_TARGET" "$REMOTE_DIR"
else
  rsync -az -e "ssh ${SSH_ARGS[*]}" "$ROOT_DIR/assets/languages/" "$REMOTE_TARGET:$REMOTE_DIR/assets/languages/"
fi

if [[ -d "$ROOT_DIR/artifacts/zip" ]]; then
  log "Sync artifacts/zip..."
  if [[ "$DRY_RUN" != "true" ]]; then
    rsync -az -e "ssh ${SSH_ARGS[*]}" "$ROOT_DIR/artifacts/zip/" "$REMOTE_TARGET:$REMOTE_DIR/artifacts/zip/"
  fi
fi

# ── 4. Transfert du bundle d'images ────────────────────────────────────────
log "Transfert du bundle vers le VPS..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m scp %s %s:%s/\n' "$BUNDLE_FILE" "$REMOTE_TARGET" "$REMOTE_DIR"
else
  scp "${SSH_ARGS[@]}" "$BUNDLE_FILE" "$REMOTE_TARGET:$REMOTE_DIR/"
  rm -f "$BUNDLE_FILE"
  log "Bundle local supprimé"
fi

# ── 5. Chargement et déploiement sur le VPS ────────────────────────────────
REMOTE_SCRIPT=$(cat <<EOR
set -euo pipefail
REMOTE_DIR="$REMOTE_DIR"
DEPLOY_TAG="$DEPLOY_TAG"
DEPLOY_ENV="$DEPLOY_ENV"
BUNDLE="\$REMOTE_DIR/lama-images-\$DEPLOY_TAG.tar.gz"

echo "[VPS] Chargement des images..."
docker load -i "\$BUNDLE"
rm -f "\$BUNDLE"

echo "[VPS] Arrêt des stacks depuis les anciens répertoires..."
for OLD_DIR in "/srv/playalama/\$DEPLOY_ENV" "/srv/playalama"; do
  if [ -d "\$OLD_DIR" ] && ([ -f "\$OLD_DIR/docker-compose.yml" ] || [ -f "\$OLD_DIR/tools/docker/docker-compose.\$DEPLOY_ENV.yml" ]); then
    echo "  Arrêt stack depuis \$OLD_DIR..."
    cd "\$OLD_DIR" && docker compose -p "\$DEPLOY_ENV" down --remove-orphans 2>/dev/null || true
  fi
done

echo "[VPS] Arrêt des anciens containers nommés explicitement..."
for c in lama-portal-webapp-\$DEPLOY_ENV lama-game-webapp-\$DEPLOY_ENV lama-portal-webapp lama-game-webapp; do
  docker rm -f "\$c" 2>/dev/null && echo "  Supprimé: \$c" || true
done

echo "[VPS] Vérification fichier .env..."
if [ ! -f "\$REMOTE_DIR/.env" ]; then
  echo "  ⚠  AVERTISSEMENT: \$REMOTE_DIR/.env introuvable — les variables d'environnement seront manquantes!"
  echo "  Créez le fichier .env avec: scp .env.prod debian@playalama.online:\$REMOTE_DIR/.env"
fi

echo "[VPS] Démarrage avec les nouvelles images..."
cd "\$REMOTE_DIR"
DEPLOY_TAG="\$DEPLOY_TAG" docker compose -p "\$DEPLOY_ENV" \
  -f "\$REMOTE_DIR/docker-compose.yml" \
  up -d --no-build --remove-orphans

echo "[VPS] Containers actifs:"
docker ps --format "  {{.Names}} ({{.Status}})" | grep lama || true
EOR
)

log "Chargement et déploiement sur le VPS..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m ssh %s <<SCRIPT\n%s\nSCRIPT\n' "$REMOTE_TARGET" "$REMOTE_SCRIPT"
else
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$REMOTE_SCRIPT"
fi

# ── 6. Nettoyage VPS (anciens containers/images obsolètes) ─────────────────
if [[ "$SKIP_CLEANUP" == "false" ]]; then
  log "Nettoyage des anciennes images Docker sur le VPS..."
  CLEANUP_SCRIPT=$(cat <<EOR
echo "[VPS] Suppression containers obsolètes (portal/game webapp anciens)..."
for c in lama-portal-webapp-$DEPLOY_ENV lama-game-webapp-$DEPLOY_ENV; do
  docker rm -f "\$c" 2>/dev/null && echo "  Supprimé: \$c" || true
done
echo "[VPS] Nettoyage images dangling..."
docker image prune -f
echo "[VPS] Suppression des anciennes images lama-server/lama-webapp (garder 2)..."
for img in lama-server lama-webapp; do
  docker images "\$img" --format '{{.Tag}}' | sort -r | tail -n +3 | \
    xargs -I{} docker rmi "\$img:{}" 2>/dev/null || true
done
EOR
  )
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m # Nettoyage VPS\n'
  else
    ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$CLEANUP_SCRIPT"
  fi
fi

# ── 7. Health check ─────────────────────────────────────────────────────────
if [[ "$SKIP_HEALTHCHECK" == "true" ]]; then
  warn "Health check ignoré"
elif [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m curl -fsS %s\n' "$HEALTH_URL"
else
  log "Health check: $HEALTH_URL"
  MAX_RETRIES=12; RETRY_DELAY=10
  for i in $(seq 1 $MAX_RETRIES); do
    if curl -fsS "$HEALTH_URL" >/dev/null 2>&1; then
      ok "Health check réussi ✓"
      break
    fi
    if [[ $i -eq $MAX_RETRIES ]]; then
      err "Health check échoué après $((MAX_RETRIES * RETRY_DELAY))s"
    fi
    log "Attente démarrage... ($i/$MAX_RETRIES)"
    sleep "$RETRY_DELAY"
  done
fi

ok "════════════════════════════════════════════════════"
ok " Déploiement $DEPLOY_ENV terminé 🦙  tag=$DEPLOY_TAG"
ok " → $HEALTH_URL"
ok "════════════════════════════════════════════════════"
