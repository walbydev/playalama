#!/usr/bin/env bash
# =============================================================================
# deploy-env.sh — Déploiement d'un environnement sur le VPS
# =============================================================================
# Usage:
#   bash tools/scripts/deploy/deploy-env.sh --env prod   --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/deploy/deploy-env.sh --env staging --ssh-key ~/.ssh/playalama.key
#
# Options:
#   --env <prod|staging>    Environnement cible (obligatoire)
#   --ssh-key <file>        Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --branch <name>         Branche git à déployer (défaut: master pour prod et staging)
#   --target <user@host>    Cible SSH (défaut: debian@playalama.online)
#   --skip-healthcheck      Ne pas vérifier l'état après déploiement
#   --dry-run               Afficher les commandes sans les exécuter
#   -h, --help              Afficher cette aide
#
# Variables d'environnement :
#   LAMA_DEPLOY_SSH_KEY     Chemin vers la clé SSH
#   LAMA_DEPLOY_TARGET      Cible SSH (user@host)
# =============================================================================
set -euo pipefail

DEPLOY_ENV=""
SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
BRANCH=""
SKIP_HEALTHCHECK=false
DRY_RUN=false

usage() {
  grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \?//'
  exit 0
}

log() { printf '\033[0;36m[DEPLOY]\033[0m %s\n' "$*"; }
err() { printf '\033[0;31m[DEPLOY][ERREUR]\033[0m %s\n' "$*" >&2; exit 1; }
ok()  { printf '\033[0;32m[DEPLOY][OK]\033[0m %s\n' "$*"; }

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
    --env)        DEPLOY_ENV="$2"; shift 2 ;;
    --ssh-key)    SSH_KEY_FILE="$2"; shift 2 ;;
    --target)     REMOTE_TARGET="$2"; shift 2 ;;
    --branch)     BRANCH="$2"; shift 2 ;;
    --skip-healthcheck) SKIP_HEALTHCHECK=true; shift ;;
    --dry-run)    DRY_RUN=true; shift ;;
    -h|--help)    usage ;;
    *) err "Option inconnue: $1" ;;
  esac
done

# Validation
[[ -z "$DEPLOY_ENV" ]] && err "Option --env obligatoire (prod ou staging)"
[[ "$DEPLOY_ENV" != "prod" && "$DEPLOY_ENV" != "staging" ]] && err "--env doit être 'prod' ou 'staging'"

# Configuration selon l'environnement
case "$DEPLOY_ENV" in
  prod)
    REMOTE_DIR="/srv/playalama/prod"
    COMPOSE_FILE="tools/docker/docker-compose.prod.yml"
    HEALTH_URL="https://playalama.online/health"
    DEFAULT_BRANCH="master"
    ;;
  staging)
    REMOTE_DIR="/srv/playalama/staging"
    COMPOSE_FILE="tools/docker/docker-compose.staging.yml"
    HEALTH_URL="https://staging.playalama.online/health"
    DEFAULT_BRANCH="master"
    ;;
esac

[[ -z "$BRANCH" ]] && BRANCH="$DEFAULT_BRANCH"

# Arguments SSH
SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

# Vérification des outils requis
command -v ssh >/dev/null 2>&1 || err "ssh non trouvé"

log "════════════════════════════════════════"
log " Déploiement LAMA — environnement: $(echo "$DEPLOY_ENV" | tr '[:lower:]' '[:upper:]')"
log " Cible SSH : $REMOTE_TARGET"
log " Répertoire : $REMOTE_DIR"
log " Branche git : $BRANCH"
log "════════════════════════════════════════"

# 1. Vérifier que le répertoire de déploiement existe sur le VPS
log "Vérification du répertoire distant..."
run_remote "test -d '$REMOTE_DIR' || (echo 'Répertoire $REMOTE_DIR inexistant — lancer make setup-vps d''abord' >&2 && exit 1)"

# 2. Vérifier que le .env existe
run_remote "test -f '$REMOTE_DIR/.env' || (echo 'Fichier .env manquant dans $REMOTE_DIR — voir tools/docker/.env.$DEPLOY_ENV.example' >&2 && exit 1)"

# 3. Git pull (cert auto-signé Gitea)
log "Git pull (branche $BRANCH)..."
run_remote "cd '$REMOTE_DIR' && GIT_SSL_NO_VERIFY=true git fetch origin && git checkout '$BRANCH' && GIT_SSL_NO_VERIFY=true git pull origin '$BRANCH'"

# 4. Docker compose up --build
log "Build et démarrage des containers..."
run_remote "cd '$REMOTE_DIR' && docker compose -f '$COMPOSE_FILE' up -d --build --remove-orphans"

# 5. Healthcheck
if [[ "$SKIP_HEALTHCHECK" == "true" ]]; then
  log "Health check ignoré (--skip-healthcheck)"
elif [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m curl -fsS %s\n' "$HEALTH_URL"
else
  log "Health check: $HEALTH_URL"
  MAX_RETRIES=12
  RETRY_DELAY=10
  for i in $(seq 1 $MAX_RETRIES); do
    if curl -fsS "$HEALTH_URL" >/dev/null 2>&1; then
      ok "Health check réussi ✓"
      break
    fi
    if [[ $i -eq $MAX_RETRIES ]]; then
      err "Health check échoué après $((MAX_RETRIES * RETRY_DELAY))s — vérifier les logs: make logs-$DEPLOY_ENV SSH_KEY=..."
    fi
    log "Attente démarrage... ($i/$MAX_RETRIES)"
    sleep "$RETRY_DELAY"
  done
fi

ok "════════════════════════════════════════"
ok " Déploiement $DEPLOY_ENV terminé avec succès 🦙"
ok " → $HEALTH_URL"
ok "════════════════════════════════════════"
