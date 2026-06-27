#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
REMOTE_BASE_DIR="${LAMA_DEPLOY_REMOTE_BASE:-/opt/playalama}"
REMOTE_DATA_DIR="${LAMA_DEPLOY_REMOTE_DATA:-/srv/playalama}"
SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
LETSENCRYPT_EMAIL="${LAMA_DEPLOY_LE_EMAIL:-admin@playalama.online}"
CERTBOT_DOMAINS_RAW="${LAMA_DEPLOY_CERTBOT_DOMAINS:-playalama.online}"
DEPLOY_TAG="${LAMA_DEPLOY_TAG:-$(date +%Y%m%d-%H%M%S)}"
SKIP_CERTBOT="false"
SKIP_HEALTHCHECK="false"
ULTRA_SAFE="false"
DRY_RUN="false"
VERBOSE="false"

usage() {
  cat <<'EOF'
Usage: tools/scripts/deploy/deploy-oneclick-images.sh [options]

Build local Docker images (server + webapp), ship them to VPS via scp/rsync,
load images remotely, and redeploy with docker compose. No source code on VPS.

Options:
  --target <user@host>           SSH target (default: debian@playalama.online)
  --remote-base <path>           App directory on VPS (default: /opt/playalama)
  --remote-data <path>           Persistent data directory (default: /srv/playalama)
  --ssh-key <file>               SSH private key file
  --tag <value>                  Image tag suffix (default: timestamp)
  --email <addr>                 Let's Encrypt email (default: admin@playalama.online)
  --certbot-domains <csv>        Domains for certbot (default: playalama.online)
  --skip-certbot                 Skip certbot run
  --skip-healthcheck             Skip final HTTP checks
  --ultra-safe                   Enable ACME challenge preflight before certbot
  --dry-run                      Print actions without executing
  --verbose                      Verbose command logs
  -h, --help                     Show help

Domain expected by nginx/certbot:
  playalama.online
EOF
}

log()  { printf '\033[0;36m[DEPLOY]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[WARN]\033[0m %s\n' "$*" >&2; }
err()  { printf '\033[0;31m[ERR]\033[0m %s\n' "$*" >&2; exit 1; }

run_cmd() {
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m %s\n' "$*"
    return 0
  fi
  [[ "$VERBOSE" == "true" ]] && printf '\033[0;35m[CMD]\033[0m %s\n' "$*"
  eval "$*"
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || err "Commande manquante: $1"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)         REMOTE_TARGET="$2"; shift 2 ;;
    --remote-base)    REMOTE_BASE_DIR="$2"; shift 2 ;;
    --remote-data)    REMOTE_DATA_DIR="$2"; shift 2 ;;
    --ssh-key)        SSH_KEY_FILE="$2"; shift 2 ;;
    --tag)            DEPLOY_TAG="$2"; shift 2 ;;
    --email)          LETSENCRYPT_EMAIL="$2"; shift 2 ;;
    --certbot-domains) CERTBOT_DOMAINS_RAW="$2"; shift 2 ;;
    --skip-certbot)   SKIP_CERTBOT="true"; shift ;;
    --skip-healthcheck) SKIP_HEALTHCHECK="true"; shift ;;
    --ultra-safe)     ULTRA_SAFE="true"; shift ;;
    --dry-run)        DRY_RUN="true"; shift ;;
    --verbose)        VERBOSE="true"; shift ;;
    -h|--help)        usage; exit 0 ;;
    *) err "Option inconnue: $1" ;;
  esac
done

SSH_ARGS=(-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10)
if [[ -n "$SSH_KEY_FILE" ]]; then
  SSH_KEY_FILE="${SSH_KEY_FILE/#\~/$HOME}"
  [[ ! -f "$SSH_KEY_FILE" ]] && err "Clé SSH introuvable: $SSH_KEY_FILE"
  SSH_ARGS=(-i "$SSH_KEY_FILE" "${SSH_ARGS[@]}")
fi

require_cmd docker
require_cmd ssh
require_cmd rsync
require_cmd gzip
require_cmd tar
require_cmd curl

[[ ! -f "$ROOT_DIR/tools/docker/docker-compose.prod.yml" ]] && \
  err "Fichier manquant: tools/docker/docker-compose.prod.yml"

log "Preflight SSH"
run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'echo connected >/dev/null'"

resolve_domain() {
  getent ahostsv4 "$1" 2>/dev/null | awk 'NR==1{print $1}'
}

IFS=',' read -r -a CERTBOT_DOMAINS <<<"$CERTBOT_DOMAINS_RAW"
RESOLVED_DOMAINS=()
MISSING_DOMAINS=()
for domain in "${CERTBOT_DOMAINS[@]}"; do
  domain="${domain// /}"
  [[ -z "$domain" ]] && continue
  if ip="$(resolve_domain "$domain")" && [[ -n "$ip" ]]; then
    RESOLVED_DOMAINS+=("$domain")
  else
    MISSING_DOMAINS+=("$domain")
  fi
done

[[ ${#MISSING_DOMAINS[@]} -gt 0 ]] && warn "DNS non résolu pour: ${MISSING_DOMAINS[*]}"
[[ ${#RESOLVED_DOMAINS[@]} -eq 0 ]] && { warn "Aucun domaine certbot résolu: certbot ignoré"; SKIP_CERTBOT="true"; }

STAGE_DIR="$ROOT_DIR/.deploy/oneclick-$DEPLOY_TAG"
BUNDLE_FILE="$STAGE_DIR/lama-images-$DEPLOY_TAG.tar.gz"
mkdir -p "$STAGE_DIR/tools/docker"

SERVER_IMAGE="lama-server:$DEPLOY_TAG"
WEBAPP_IMAGE="lama-webapp:$DEPLOY_TAG"

# ── Build images ──────────────────────────────────────────────────────────────
log "Build image serveur ($SERVER_IMAGE)..."
run_cmd "cd '$ROOT_DIR' && docker build -f tools/docker/Dockerfile.server -t '$SERVER_IMAGE' ."

log "Build image webapp ($WEBAPP_IMAGE)..."
run_cmd "cd '$ROOT_DIR' && docker build -f tools/docker/Dockerfile.webapp -t '$WEBAPP_IMAGE' ."

# ── Pack images ───────────────────────────────────────────────────────────────
log "Pack des images → $BUNDLE_FILE..."
run_cmd "cd '$ROOT_DIR' && docker save '$SERVER_IMAGE' '$WEBAPP_IMAGE' | gzip > '$BUNDLE_FILE'"

# ── Prépare fichiers de déploiement ──────────────────────────────────────────
log "Préparation des fichiers de déploiement..."
cp "$ROOT_DIR/tools/docker/docker-compose.prod.yml" "$STAGE_DIR/docker-compose.yml"
[[ -f "$ROOT_DIR/tools/docker/nginx-downloads.conf" ]] && \
  cp "$ROOT_DIR/tools/docker/nginx-downloads.conf" "$STAGE_DIR/tools/docker/nginx-downloads.conf"

[[ -d "$ROOT_DIR/assets/languages" ]] && \
  { mkdir -p "$STAGE_DIR/assets"; cp -a "$ROOT_DIR/assets/languages" "$STAGE_DIR/assets/languages"; }

[[ -d "$ROOT_DIR/artifacts/zip" ]] && \
  { mkdir -p "$STAGE_DIR/artifacts"; cp -a "$ROOT_DIR/artifacts/zip" "$STAGE_DIR/artifacts/zip"; }

# Fichier de tags d'images pour l'override docker compose
cat > "$STAGE_DIR/docker-compose.images.yml" <<EOF
services:
  lama-server-prod:
    image: $SERVER_IMAGE
  lama-webapp-prod:
    image: $WEBAPP_IMAGE
EOF

# ── Sync VPS ──────────────────────────────────────────────────────────────────
log "Sync VPS: $REMOTE_TARGET"
run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'mkdir -p \"$REMOTE_BASE_DIR/prod/tools/docker\" \"$REMOTE_BASE_DIR/prod/assets\" \"$REMOTE_BASE_DIR/prod/artifacts\"'"
run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/docker-compose.yml' '$REMOTE_TARGET:$REMOTE_BASE_DIR/prod/docker-compose.yml'"
run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/docker-compose.images.yml' '$REMOTE_TARGET:$REMOTE_BASE_DIR/prod/docker-compose.images.yml'"

if [[ -f "$STAGE_DIR/tools/docker/nginx-downloads.conf" ]]; then
  run_cmd "rsync -az -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/tools/docker/nginx-downloads.conf' '$REMOTE_TARGET:$REMOTE_BASE_DIR/prod/tools/docker/nginx-downloads.conf'"
fi

if [[ -d "$STAGE_DIR/assets/languages" ]]; then
  run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/assets/languages/' '$REMOTE_TARGET:$REMOTE_BASE_DIR/prod/assets/languages/'"
fi

if [[ -d "$STAGE_DIR/artifacts/zip" ]]; then
  run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/artifacts/zip/' '$REMOTE_TARGET:$REMOTE_BASE_DIR/prod/artifacts/zip/'"
fi

run_cmd "rsync -az -e 'ssh ${SSH_ARGS[*]}' '$BUNDLE_FILE' '$REMOTE_TARGET:$REMOTE_BASE_DIR/prod/'"

# ── Remote deploy script ──────────────────────────────────────────────────────
REMOTE_SCRIPT=$(cat <<'EOS'
set -euo pipefail

REMOTE_DIR="__REMOTE_BASE_DIR__/prod"
DEPLOY_TAG="__DEPLOY_TAG__"
BUNDLE_FILE="$REMOTE_DIR/lama-images-$DEPLOY_TAG.tar.gz"

cd "$REMOTE_DIR"

echo "[VPS] Chargement des images..."
docker load -i "$BUNDLE_FILE"
rm -f "$BUNDLE_FILE"

echo "[VPS] Déploiement des conteneurs..."
DEPLOY_TAG="$DEPLOY_TAG" docker compose \
  -f docker-compose.yml \
  -f docker-compose.images.yml \
  up -d --no-build --remove-orphans

echo "[VPS] Nettoyage images dangling..."
docker image prune -f
EOS
)

REMOTE_SCRIPT="${REMOTE_SCRIPT//__REMOTE_BASE_DIR__/$REMOTE_BASE_DIR}"
REMOTE_SCRIPT="${REMOTE_SCRIPT//__DEPLOY_TAG__/$DEPLOY_TAG}"

log "Déploiement conteneurs..."
if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "bash -s" <<EOS\n%s\nEOS\n' "$REMOTE_TARGET" "$REMOTE_SCRIPT"
else
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$REMOTE_SCRIPT"
fi

# ── Certbot ───────────────────────────────────────────────────────────────────
if [[ "$SKIP_CERTBOT" != "true" ]]; then
  CERTBOT_ARGS=()
  for domain in "${RESOLVED_DOMAINS[@]}"; do
    CERTBOT_ARGS+=("-d" "$domain")
  done

  if [[ "$ULTRA_SAFE" == "true" ]]; then
    ACME_TOKEN="lama-preflight-$DEPLOY_TAG"
    REMOTE_ACME_DIR="$REMOTE_DATA_DIR/.deploy/certbot-webroot/.well-known/acme-challenge"
    log "Ultra-safe: preflight ACME challenge..."
    if [[ "$DRY_RUN" != "true" ]]; then
      ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "mkdir -p '$REMOTE_ACME_DIR' && printf 'ok' > '$REMOTE_ACME_DIR/$ACME_TOKEN'"
      PRECHECK_OK="true"
      for domain in "${RESOLVED_DOMAINS[@]}"; do
        curl -fsS --max-time 15 "http://$domain/.well-known/acme-challenge/$ACME_TOKEN" >/dev/null || \
          { warn "Preflight ACME échoué pour $domain"; PRECHECK_OK="false"; }
      done
      ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "rm -f '$REMOTE_ACME_DIR/$ACME_TOKEN'" || true
      [[ "$PRECHECK_OK" != "true" ]] && { warn "Preflight invalide, certbot ignoré"; SKIP_CERTBOT="true"; }
    fi
  fi

  if [[ "$SKIP_CERTBOT" != "true" ]]; then
    CERTBOT_CMD="cd '$REMOTE_BASE_DIR/prod' && docker compose -f docker-compose.yml run --rm certbot certonly --webroot -w /var/www/certbot ${CERTBOT_ARGS[*]} --non-interactive --agree-tos -m '$LETSENCRYPT_EMAIL'"
    log "Run certbot..."
    if [[ "$DRY_RUN" == "true" ]]; then
      printf '\033[0;33m[DRY-RUN]\033[0m ssh %s %q\n' "$REMOTE_TARGET" "$CERTBOT_CMD"
    else
      set +e; ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "$CERTBOT_CMD"; CERTBOT_EXIT=$?; set -e
      [[ $CERTBOT_EXIT -ne 0 ]] && { warn "Certbot échoué (code=$CERTBOT_EXIT)"; SKIP_HEALTHCHECK="true"; }
    fi
  fi
else
  warn "Certbot ignoré (--skip-certbot)"
fi

# ── Health checks ─────────────────────────────────────────────────────────────
if [[ "$SKIP_HEALTHCHECK" != "true" ]]; then
  log "Health checks..."
  run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'docker exec lama-server-prod curl -fsS http://127.0.0.1:5201/health >/dev/null'"
  run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'docker exec lama-webapp-prod curl -fsS --max-time 10 http://127.0.0.1:5001/ >/dev/null'"
  if [[ "$SKIP_CERTBOT" != "true" ]]; then
    run_cmd "curl -fsS --max-time 15 https://playalama.online/ >/dev/null"
    run_cmd "curl -fsS --max-time 15 https://playalama.online/health >/dev/null"
  fi
else
  warn "Health check ignoré (--skip-healthcheck)"
fi

log "✅ Déploiement terminé: tag=$DEPLOY_TAG"
