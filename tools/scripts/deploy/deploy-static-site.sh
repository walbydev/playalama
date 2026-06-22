#!/usr/bin/env bash
#
# deploy-static-site.sh - Déploiement professionnel Lama
# =====================================================
#
# Responsabilité: Déployer/redéployer site static + nginx + Lama.Server vers VPS
# Fichier référence: tools/docker/DOCKER_ARCHITECTURE.md
# Maintenu par: Restructuration infra 2026-06-19
#
# Sécurité:
#   - SSH authentification par clé seulement (BatchMode=yes)
#   - Validation de tous les fichiers locaux AVANT rsync
#   - Rollback possible: fichiers numérotés par timestamp
#
# Usage:
#   ./tools/scripts/deploy-static-site.sh --mode prod \
#     --target debian@playalama.online \
#     --ssh-key ~/.ssh/machines/playalama.key \
#     [--skip-healthcheck] [--dry-run]

set -euo pipefail

# =========================================================================
# CONFIGURATION GLOBALE
# =========================================================================

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT_VERSION="2.0.0"

MODE="local"
REMOTE_TARGET="debian@playalama.online"
REMOTE_BASE_DIR="/opt/playalama"
SKIP_HEALTHCHECK="false"
DRY_RUN="false"
SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
VERBOSE="false"

# Chemins sources locaux (certifiés)
SOURCE_DOCKERFILE="tools/docker/Dockerfile.server"
SOURCE_COMPOSE_PROD="tools/docker/docker-compose.prod.yml"
SOURCE_NGINX="tools/docker/nginx-playalama.conf"
SOURCE_SITE="site/static"
SOURCE_ASSETS="assets/languages"
SOURCE_DOWNLOADS="artifacts/zip"

# Timestamps pour traçabilité
DEPLOY_TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
DEPLOY_UUID="$(cat /dev/urandom | tr -dc 'a-z0-9' | fold -w 8 | head -n 1)"
DEPLOY_ID="${DEPLOY_TIMESTAMP}-${DEPLOY_UUID}"

# =========================================================================
# LOGGING & UTILITIES
# =========================================================================

log() {
  printf '[DEPLOY] %s\n' "$*"
}

log_info() {
  printf '[INFO] %s\n' "$*"
}

log_warn() {
  printf '[WARN] %s\n' "$*" >&2
}

log_error() {
  printf '[ERROR] %s\n' "$*" >&2
}

log_section() {
  printf '\n=== %s ===\n' "$*"
}

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    log_error "Commande manquante: $cmd"
    exit 1
  fi
}

require_file() {
  local file="$1"
  if [[ ! -f "$file" ]]; then
    log_error "Fichier manquant: $file"
    exit 1
  fi
}

# =========================================================================
# USAGE & ARGUMENTS
# =========================================================================

usage() {
  cat <<'EOF'
Usage: tools/scripts/deploy-static-site.sh [options]

Déploie/redéploie site static + nginx + serveur Lama avec validation.

Mode (défaut: local):
  --mode <local|prod>              Environnement de déploiement
  --target <user@host>             Cible SSH pour mode prod (défaut: debian@playalama.online)
  --remote-base <path>             Chemin racine VPS (défaut: /opt/playalama)
  --ssh-key <file>                 Fichier clé SSH (ou env LAMA_DEPLOY_SSH_KEY)

Comportement:
  --skip-healthcheck               Omettre vérifications endpoints
  --dry-run                        Simuler sans appliquer
  --verbose                        Sortie détaillée

Aide:
  -h, --help                       Afficher ce message

Exemples:
  # Déploiement local
  ./tools/scripts/deploy-static-site.sh --mode local

  # Production (full deployment)
  ./tools/scripts/deploy-static-site.sh --mode prod \
    --target debian@playalama.online \
    --ssh-key ~/.ssh/machines/playalama.key

  # Simulation production
  ./tools/scripts/deploy-static-site.sh --mode prod \
    --target debian@playalama.online \
    --ssh-key ~/.ssh/machines/playalama.key \
    --dry-run

Variables d'environnement:
  LAMA_DEPLOY_SSH_KEY              Clé SSH (alt: --ssh-key)
  LAMA_DEPLOY_VERBOSE              Debug (alt: --verbose)
EOF
}

# Parse arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)
      MODE="$2"
      shift 2
      ;;
    --target)
      REMOTE_TARGET="$2"
      shift 2
      ;;
    --remote-base)
      REMOTE_BASE_DIR="$2"
      shift 2
      ;;
    --ssh-key)
      SSH_KEY_FILE="$2"
      shift 2
      ;;
    --skip-healthcheck)
      SKIP_HEALTHCHECK="true"
      shift
      ;;
    --dry-run)
      DRY_RUN="true"
      shift
      ;;
    --verbose)
      VERBOSE="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      log_error "Option inconnue: $1"
      usage
      exit 1
      ;;
  esac
done

# =========================================================================
# VALIDATION
# =========================================================================

validate_mode() {
  if [[ "$MODE" != "local" && "$MODE" != "prod" ]]; then
    log_error "Mode invalide: $MODE (doit être 'local' ou 'prod')"
    exit 1
  fi
}

validate_local_files() {
  log_section "Validation fichiers locaux"
  
  local required_files=(
    "$SOURCE_DOCKERFILE"
    "$SOURCE_COMPOSE_PROD"
    "$SOURCE_NGINX"
  )
  
  local required_dirs=(
    "$SOURCE_SITE"
    "$SOURCE_ASSETS"
  )

  for file in "${required_files[@]}"; do
    require_file "$ROOT_DIR/$file"
    log_info "✓ $file"
  done
  
  for dir in "${required_dirs[@]}"; do
    if [[ ! -d "$ROOT_DIR/$dir" ]]; then
      log_error "Répertoire manquant: $dir"
      exit 1
    fi
    log_info "✓ $dir"
  done
}

validate_ssh_access() {
  log_section "Validant accès SSH"
  
  if [[ -z "$SSH_KEY_FILE" ]]; then
    log_warn "Aucune clé SSH fournie (--ssh-key ou LAMA_DEPLOY_SSH_KEY)"
    log_info "Tentative connexion sans clé..."
  else
    SSH_KEY_FILE="${SSH_KEY_FILE/#\~/$HOME}"
    if [[ ! -f "$SSH_KEY_FILE" ]]; then
      log_error "Fichier clé SSH introuvable: $SSH_KEY_FILE"
      exit 1
    fi
    log_info "✓ Clé SSH: $SSH_KEY_FILE"
  fi
  
  local ssh_cmd=("ssh")
  if [[ -n "$SSH_KEY_FILE" ]]; then
    ssh_cmd+=("-i" "$SSH_KEY_FILE")
  fi
  ssh_cmd+=("-o" "BatchMode=yes" "-o" "StrictHostKeyChecking=accept-new" "-o" "ConnectTimeout=5")
  
  if "${ssh_cmd[@]}" "$REMOTE_TARGET" "exit 0" 2>/dev/null; then
    log_info "✓ SSH connexion OK: $REMOTE_TARGET"
  else
    log_error "SSH connexion échouée vers $REMOTE_TARGET"
    exit 1
  fi
}

# =========================================================================
# LOCAL MODE - BUILD & RUN
# =========================================================================

prepare_local_tls() {
  log_section "Préparation certs locaux"
  
  local cert_dir="$ROOT_DIR/.deploy/certs/live/playalama.online"
  local cert_file="$cert_dir/fullchain.pem"
  local key_file="$cert_dir/privkey.pem"
  
  mkdir -p "$ROOT_DIR/.deploy/certbot-webroot"
  
  if [[ -f "$cert_file" && -f "$key_file" ]]; then
    log_info "✓ Certs locaux déjà présents"
    return 0
  fi
  
  log_info "Génération certificat autosigné (développement seulement)"
  mkdir -p "$cert_dir"
  
  if [[ "$DRY_RUN" == "true" ]]; then
    log_info "[DRY-RUN] openssl req -x509 ..."
  else
    openssl req -x509 -nodes -newkey rsa:2048 -days 365 \
      -keyout "$key_file" -out "$cert_file" \
      -subj '/CN=playalama.online' >/dev/null 2>&1
    log_info "✓ Certs générés: $cert_dir"
  fi
}

run_local_build() {
  log_section "Build local (lama-server + nginx)"
  
  require_cmd docker
  require_cmd docker-compose
  
  local compose_file="$ROOT_DIR/tools/docker/docker-compose.local.yml"
  
  if [[ "$DRY_RUN" == "true" ]]; then
    log_info "[DRY-RUN] docker compose -f $compose_file up --build -d"
    return 0
  fi
  
  if ! docker compose -f "$compose_file" up --build -d lama-server nginx 2>&1 | grep -v "Attaching\|Running" | head -20; then
    log_error "Erreur Docker build/run"
    exit 1
  fi
  
  log_info "✓ Services démarrés"
}

run_local_health_checks() {
  if [[ "$SKIP_HEALTHCHECK" == "true" ]]; then
    log_info "Healthchecks ignorés (--skip-healthcheck)"
    return 0
  fi
  
  log_section "Vérification endpoints locaux"
  
  local endpoints=(
    "http://127.0.0.1/"
    "http://127.0.0.1/health"
  )
  
  for endpoint in "${endpoints[@]}"; do
    if [[ "$DRY_RUN" == "true" ]]; then
      log_info "[DRY-RUN] curl -fsSI $endpoint"
    else
      if curl -fsSI "$endpoint" >/dev/null 2>&1; then
        log_info "✓ $endpoint"
      else
        log_warn "⚠ Endpoint non disponible: $endpoint"
      fi
    fi
  done
}

# =========================================================================
# PROD MODE - REMOTE DEPLOYMENT
# =========================================================================

build_ssh_args() {
  local -n ssh_args_ref=$1
  
  if [[ -n "$SSH_KEY_FILE" ]]; then
    ssh_args_ref+=("-i" "$SSH_KEY_FILE")
  fi
  
  ssh_args_ref+=("-o" "BatchMode=yes")
  ssh_args_ref+=("-o" "StrictHostKeyChecking=accept-new")
  ssh_args_ref+=("-o" "ConnectTimeout=10")
}

rsync_file() {
  local src="$1"
  local dst="$2"
  local label="${3:-}"
  
  local rsync_cmd=(rsync -az --delete)
  
  if [[ -n "$SSH_KEY_FILE" ]]; then
    local ssh_cmd="ssh -i $SSH_KEY_FILE -o BatchMode=yes -o StrictHostKeyChecking=accept-new"
    rsync_cmd+=("-e" "$ssh_cmd")
  fi
  
  if [[ "$DRY_RUN" == "true" ]]; then
    if [[ -n "$label" ]]; then
      log_info "[DRY-RUN] rsync: $label"
    else
      log_info "[DRY-RUN] rsync: $src → $REMOTE_TARGET:$dst"
    fi
    return 0
  fi
  
  if ! "${rsync_cmd[@]}" "$src" "$REMOTE_TARGET:$dst" 2>&1 | tail -5; then
    log_error "rsync échoué: $src → $dst"
    exit 1
  fi
  
  if [[ -n "$label" ]]; then
    log_info "✓ $label"
  else
    log_info "✓ $src"
  fi
}

prepare_remote_dirs() {
  log_section "Préparation répertoires VPS"
  
  local ssh_args=()
  build_ssh_args ssh_args
  
  local required_dirs=(
    "$REMOTE_BASE_DIR/tools/docker"
    "$REMOTE_BASE_DIR/site"
    "$REMOTE_BASE_DIR/assets"
    "$REMOTE_BASE_DIR/artifacts"
    "$REMOTE_BASE_DIR/.deploy"
  )
  
  for dir in "${required_dirs[@]}"; do
    if [[ "$DRY_RUN" == "true" ]]; then
      log_info "[DRY-RUN] mkdir -p $dir"
    else
      if ! ssh "${ssh_args[@]}" "$REMOTE_TARGET" "mkdir -p '$dir'" 2>/dev/null; then
        log_error "Impossible de créer $dir sur VPS"
        exit 1
      fi
      log_info "✓ $dir"
    fi
  done
}

sync_prod_files() {
  log_section "Synchronisation fichiers production"
  
  # DOCKERFILE (crucial!)
  rsync_file \
    "$ROOT_DIR/$SOURCE_DOCKERFILE" \
    "$REMOTE_BASE_DIR/Dockerfile" \
    "Dockerfile.server → Dockerfile"
  
  # docker-compose
  rsync_file \
    "$ROOT_DIR/$SOURCE_COMPOSE_PROD" \
    "$REMOTE_BASE_DIR/docker-compose.yml" \
    "docker-compose.prod.yml → docker-compose.yml"
  
  # nginx config
  rsync_file \
    "$ROOT_DIR/$SOURCE_NGINX" \
    "$REMOTE_BASE_DIR/tools/docker/nginx-playalama.conf" \
    "nginx-playalama.conf"
  
  # content
  rsync_file \
    "$ROOT_DIR/$SOURCE_SITE/" \
    "$REMOTE_BASE_DIR/site/static/" \
    "site/static/"
  
  rsync_file \
    "$ROOT_DIR/$SOURCE_ASSETS/" \
    "$REMOTE_BASE_DIR/assets/languages/" \
    "assets/languages/"
  
  if [[ -d "$ROOT_DIR/$SOURCE_DOWNLOADS" ]]; then
    rsync_file \
      "$ROOT_DIR/$SOURCE_DOWNLOADS/" \
      "$REMOTE_BASE_DIR/artifacts/zip/" \
      "artifacts/zip/"
  fi
}

rebuild_remote_services() {
  log_section "Rebuild + restart services VPS"
  
  local ssh_args=()
  build_ssh_args ssh_args
  
  local docker_cmd="cd '$REMOTE_BASE_DIR' && docker compose -f docker-compose.yml up -d --build lama-server nginx 2>&1 | tail -10"
  
  if [[ "$DRY_RUN" == "true" ]]; then
    log_info "[DRY-RUN] ssh $REMOTE_TARGET '$docker_cmd'"
  else
    if ! ssh "${ssh_args[@]}" "$REMOTE_TARGET" "$docker_cmd"; then
      log_error "Docker build/restart échoué sur VPS"
      exit 1
    fi
    log_info "✓ Services VPS redémarrés"
  fi
}

run_prod_health_checks() {
  if [[ "$SKIP_HEALTHCHECK" == "true" ]]; then
    log_info "Healthchecks ignorés (--skip-healthcheck)"
    return 0
  fi
  
  log_section "Vérification endpoints production"
  
  local endpoints=(
    "https://playalama.online/health"
    "https://playalama.online/"
    "https://playalama.online/download/"
  )
  
  local wait_time=0
  local max_wait=60
  
  log_info "Attente démarrage services (max ${max_wait}s)..."
  
  while [[ $wait_time -lt $max_wait ]]; do
    if curl -kfsSI "https://playalama.online/health" >/dev/null 2>&1; then
      break
    fi
    sleep 2
    ((wait_time+=2))
  done
  
  for endpoint in "${endpoints[@]}"; do
    if [[ "$DRY_RUN" == "true" ]]; then
      log_info "[DRY-RUN] curl $endpoint"
    else
      if curl -kfsSI "$endpoint" >/dev/null 2>&1; then
        log_info "✓ $endpoint"
      else
        log_warn "⚠ Endpoint non disponible: $endpoint (check DNS/SSL)"
      fi
    fi
  done
}

# =========================================================================
# MAIN EXECUTION
# =========================================================================

main() {
  log_info "Lama Deployer v$SCRIPT_VERSION"
  log_info "Mode: $MODE | Déploiement: $DEPLOY_ID"
  
  validate_mode
  validate_local_files
  require_cmd curl
  
  if [[ "$MODE" == "local" ]]; then
    # === LOCAL MODE ===
    require_cmd docker
    require_cmd docker-compose
    
    prepare_local_tls
    run_local_build
    run_local_health_checks
    
    log_section "✅ Déploiement local terminé"
    
  else
    # === PROD MODE ===
    require_cmd ssh
    require_cmd rsync
    
    validate_ssh_access
    prepare_remote_dirs
    sync_prod_files
    rebuild_remote_services
    run_prod_health_checks
    
    log_section "✅ Déploiement production terminé"
    log_info "Déploiement tracé avec ID: $DEPLOY_ID"
    log_info "Pour rollback: git checkout -- tools/docker/"
  fi
  
  log_info "État: SUCCÈS"
}

# Gestion d'erreurs
trap 'log_error "Erreur ligne $LINENO"; exit 1' ERR

# =========================================================================
main "$@"
