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
Usage: tools/deployments/deploy-oneclick-images.sh [options]

Build local Docker images (server + game webapp + portal webapp),
ship them to VPS via rsync, load images remotely, and redeploy with docker compose.

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

log() { printf '[DEPLOY] %s\n' "$*"; }
warn() { printf '[WARN] %s\n' "$*" >&2; }
err() { printf '[ERR] %s\n' "$*" >&2; }

run_cmd() {
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '[DRY-RUN] %s\n' "$*"
    return 0
  fi

  if [[ "$VERBOSE" == "true" ]]; then
    printf '[CMD] %s\n' "$*"
  fi

  eval "$*"
}

require_cmd() {
  local cmd="$1"
  command -v "$cmd" >/dev/null 2>&1 || {
    err "Commande manquante: $cmd"
    exit 1
  }
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      REMOTE_TARGET="$2"
      shift 2
      ;;
    --remote-base)
      REMOTE_BASE_DIR="$2"
      shift 2
      ;;
    --remote-data)
      REMOTE_DATA_DIR="$2"
      shift 2
      ;;
    --ssh-key)
      SSH_KEY_FILE="$2"
      shift 2
      ;;
    --tag)
      DEPLOY_TAG="$2"
      shift 2
      ;;
    --email)
      LETSENCRYPT_EMAIL="$2"
      shift 2
      ;;
    --certbot-domains)
      CERTBOT_DOMAINS_RAW="$2"
      shift 2
      ;;
    --skip-certbot)
      SKIP_CERTBOT="true"
      shift
      ;;
    --skip-healthcheck)
      SKIP_HEALTHCHECK="true"
      shift
      ;;
    --ultra-safe)
      ULTRA_SAFE="true"
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
      err "Option inconnue: $1"
      usage
      exit 1
      ;;
  esac
done

SSH_ARGS=(-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10)
if [[ -n "$SSH_KEY_FILE" ]]; then
  SSH_KEY_FILE="${SSH_KEY_FILE/#\~/$HOME}"
  if [[ ! -f "$SSH_KEY_FILE" ]]; then
    err "Clé SSH introuvable: $SSH_KEY_FILE"
    exit 1
  fi
  SSH_ARGS=(-i "$SSH_KEY_FILE" "${SSH_ARGS[@]}")
fi

require_cmd docker
require_cmd ssh
require_cmd rsync
require_cmd gzip
require_cmd tar
require_cmd curl

if [[ ! -f "$ROOT_DIR/tools/docker/docker-compose.prod.yml" ]]; then
  err "Fichier manquant: tools/docker/docker-compose.prod.yml"
  exit 1
fi

log "Preflight SSH"
run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'echo connected >/dev/null'"

resolve_domain() {
  local domain="$1"
  getent ahostsv4 "$domain" 2>/dev/null | awk 'NR==1{print $1}'
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

if [[ ${#MISSING_DOMAINS[@]} -gt 0 ]]; then
  warn "DNS non resolu pour: ${MISSING_DOMAINS[*]}"
fi

if [[ ${#RESOLVED_DOMAINS[@]} -eq 0 ]]; then
  warn "Aucun domaine certbot resolu: certbot sera ignore pour ce deploy"
  SKIP_CERTBOT="true"
fi

if [[ "$ULTRA_SAFE" == "true" && "$SKIP_CERTBOT" != "true" ]]; then
  log "Ultra-safe: preparation du preflight ACME"
fi

STAGE_DIR="$ROOT_DIR/.deploy/oneclick-$DEPLOY_TAG"
BUNDLE_FILE="$STAGE_DIR/lama-images-$DEPLOY_TAG.tar.gz"

mkdir -p "$STAGE_DIR/tools/docker"

SERVER_IMAGE="lama-server:$DEPLOY_TAG"
GAME_IMAGE="lama-game-webapp:$DEPLOY_TAG"
PORTAL_IMAGE="lama-portal-webapp:$DEPLOY_TAG"

log "Build images locales"
run_cmd "cd '$ROOT_DIR' && docker build -f tools/docker/Dockerfile.server -t '$SERVER_IMAGE' ."
run_cmd "cd '$ROOT_DIR' && docker build -f tools/docker/Dockerfile.webapp -t '$GAME_IMAGE' ."
run_cmd "cd '$ROOT_DIR' && docker build -f tools/docker/Dockerfile.portalwebapp -t '$PORTAL_IMAGE' ."

log "Pack images"
run_cmd "cd '$ROOT_DIR' && docker save '$SERVER_IMAGE' '$GAME_IMAGE' '$PORTAL_IMAGE' | gzip > '$BUNDLE_FILE'"

log "Prépare fichiers de déploiement"
cp "$ROOT_DIR/tools/docker/docker-compose.prod.yml" "$STAGE_DIR/docker-compose.yml"
cp "$ROOT_DIR/tools/docker/nginx-playalama.conf" "$STAGE_DIR/tools/docker/nginx-playalama.conf"

if [[ -d "$ROOT_DIR/assets/languages" ]]; then
  mkdir -p "$STAGE_DIR/assets"
  cp -a "$ROOT_DIR/assets/languages" "$STAGE_DIR/assets/languages"
fi

if [[ -d "$ROOT_DIR/artifacts/zip" ]]; then
  mkdir -p "$STAGE_DIR/artifacts"
  cp -a "$ROOT_DIR/artifacts/zip" "$STAGE_DIR/artifacts/zip"
fi

cat > "$STAGE_DIR/docker-compose.images.yml" <<EOF
services:
  lama-server:
    image: $SERVER_IMAGE
  lama-game-webapp:
    image: $GAME_IMAGE
  lama-portal-webapp:
    image: $PORTAL_IMAGE
EOF

log "Sync VPS: $REMOTE_TARGET"
run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'mkdir -p \"$REMOTE_BASE_DIR/tools/docker\" \"$REMOTE_BASE_DIR/assets\" \"$REMOTE_BASE_DIR/artifacts\" \"$REMOTE_DATA_DIR/.deploy/certs\" \"$REMOTE_DATA_DIR/.deploy/certbot-webroot\" \"$REMOTE_DATA_DIR/logs\"'"
run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/docker-compose.yml' '$REMOTE_TARGET:$REMOTE_BASE_DIR/docker-compose.yml'"
run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/docker-compose.images.yml' '$REMOTE_TARGET:$REMOTE_BASE_DIR/docker-compose.images.yml'"
run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/tools/docker/nginx-playalama.conf' '$REMOTE_TARGET:$REMOTE_BASE_DIR/tools/docker/nginx-playalama.conf'"

if [[ -d "$STAGE_DIR/assets/languages" ]]; then
  run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/assets/languages/' '$REMOTE_TARGET:$REMOTE_BASE_DIR/assets/languages/'"
fi

if [[ -d "$STAGE_DIR/artifacts/zip" ]]; then
  run_cmd "rsync -az --delete -e 'ssh ${SSH_ARGS[*]}' '$STAGE_DIR/artifacts/zip/' '$REMOTE_TARGET:$REMOTE_BASE_DIR/artifacts/zip/'"
fi

run_cmd "rsync -az -e 'ssh ${SSH_ARGS[*]}' '$BUNDLE_FILE' '$REMOTE_TARGET:$REMOTE_BASE_DIR/'"

REMOTE_SCRIPT=$(cat <<'EOS'
set -euo pipefail

REMOTE_BASE_DIR="__REMOTE_BASE_DIR__"
REMOTE_DATA_DIR="__REMOTE_DATA_DIR__"
DEPLOY_TAG="__DEPLOY_TAG__"
BUNDLE_FILE="$REMOTE_BASE_DIR/lama-images-$DEPLOY_TAG.tar.gz"

cd "$REMOTE_BASE_DIR"

ln -sfn "$REMOTE_DATA_DIR/.deploy" "$REMOTE_BASE_DIR/.deploy"
ln -sfn "$REMOTE_DATA_DIR/logs" "$REMOTE_BASE_DIR/logs"

docker load -i "$BUNDLE_FILE"

docker compose -f docker-compose.yml -f docker-compose.images.yml config >/dev/null
docker compose -f docker-compose.yml -f docker-compose.images.yml up -d --no-build --remove-orphans lama-server lama-game-webapp lama-portal-webapp nginx
EOS
)

REMOTE_SCRIPT="${REMOTE_SCRIPT//__REMOTE_BASE_DIR__/$REMOTE_BASE_DIR}"
REMOTE_SCRIPT="${REMOTE_SCRIPT//__REMOTE_DATA_DIR__/$REMOTE_DATA_DIR}"
REMOTE_SCRIPT="${REMOTE_SCRIPT//__DEPLOY_TAG__/$DEPLOY_TAG}"

log "Déploie les conteneurs"
if [[ "$DRY_RUN" == "true" ]]; then
  printf '[DRY-RUN] ssh %s <<REMOTE_SCRIPT\n%s\nREMOTE_SCRIPT\n' "$REMOTE_TARGET" "$REMOTE_SCRIPT"
else
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$REMOTE_SCRIPT"
fi

if [[ "$SKIP_CERTBOT" != "true" ]]; then
  CERTBOT_ARGS=()
  for domain in "${RESOLVED_DOMAINS[@]}"; do
    CERTBOT_ARGS+=("-d" "$domain")
  done
  CERTBOT_DOMAIN_ARGS="${CERTBOT_ARGS[*]}"

  if [[ "$ULTRA_SAFE" == "true" ]]; then
    log "Ultra-safe: preflight nginx/port 80 sur le VPS"

    if [[ "$DRY_RUN" == "true" ]]; then
      printf '[DRY-RUN] ssh %s %q\n' "$REMOTE_TARGET" "docker exec nginx-playalama nginx -t && curl -fsS http://127.0.0.1/ >/dev/null"
    else
      set +e
      ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "docker exec nginx-playalama nginx -t && curl -fsS --max-time 10 http://127.0.0.1/ >/dev/null"
      NGINX_LOCAL_EXIT=$?
      set -e

      if [[ $NGINX_LOCAL_EXIT -ne 0 ]]; then
        warn "Ultra-safe: nginx ou le port 80 local ne repond pas sur le VPS. Certbot saute pour ce deploy."
        SKIP_CERTBOT="true"
      fi
    fi
  fi

  if [[ "$ULTRA_SAFE" == "true" ]]; then
    ACME_TOKEN="lama-preflight-$DEPLOY_TAG"
    REMOTE_ACME_DIR="$REMOTE_DATA_DIR/.deploy/certbot-webroot/.well-known/acme-challenge"
    ACME_URL_PATH="/.well-known/acme-challenge/$ACME_TOKEN"

    log "Ultra-safe: preflight challenge ACME"
    if [[ "$DRY_RUN" == "true" ]]; then
      printf '[DRY-RUN] ssh %s %q\n' "$REMOTE_TARGET" "mkdir -p '$REMOTE_ACME_DIR' && printf '%s' 'ok' > '$REMOTE_ACME_DIR/$ACME_TOKEN'"
      for domain in "${RESOLVED_DOMAINS[@]}"; do
        printf '[DRY-RUN] curl http://%s%s\n' "$domain" "$ACME_URL_PATH"
      done
    else
      ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "mkdir -p '$REMOTE_ACME_DIR' && printf 'ok' > '$REMOTE_ACME_DIR/$ACME_TOKEN'"

      PRECHECK_OK="true"
      for domain in "${RESOLVED_DOMAINS[@]}"; do
        if ! curl -fsS --max-time 15 "http://$domain$ACME_URL_PATH" >/dev/null; then
          warn "ACME preflight echoue pour $domain"
          PRECHECK_OK="false"
        fi
      done

      if [[ "$PRECHECK_OK" == "true" ]]; then
        for domain in "${RESOLVED_DOMAINS[@]}"; do
          if ! curl -fsS --max-time 15 "http://$domain/" >/dev/null; then
            warn "HTTP public non joignable pour $domain (possible firewall/port 80)"
            PRECHECK_OK="false"
          fi
        done
      fi

      ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "rm -f '$REMOTE_ACME_DIR/$ACME_TOKEN'" || true

      if [[ "$PRECHECK_OK" != "true" ]]; then
        warn "Ultra-safe: preflight ACME invalide, certbot sera saute pour ce deploy"
        SKIP_CERTBOT="true"
      fi
    fi
  fi

  CERTBOT_CMD="cd '$REMOTE_BASE_DIR' && docker compose run --rm --profile certbot certbot certonly --webroot -w /var/www/certbot $CERTBOT_DOMAIN_ARGS --non-interactive --agree-tos -m '$LETSENCRYPT_EMAIL'"
  log "Run certbot"
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '[DRY-RUN] ssh %s %q\n' "$REMOTE_TARGET" "$CERTBOT_CMD"
  else
    set +e
    ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "$CERTBOT_CMD"
    CERTBOT_EXIT=$?
    set -e
    if [[ $CERTBOT_EXIT -ne 0 ]]; then
      warn "Certbot a echoue (code=$CERTBOT_EXIT). Deploiement continue sans TLS valide."
      SKIP_HEALTHCHECK="true"
    fi
  fi

  RELOAD_CMD="cd '$REMOTE_BASE_DIR' && docker compose -f docker-compose.yml -f docker-compose.images.yml up -d --no-build nginx"
  run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' '$RELOAD_CMD'"
else
  warn "Certbot ignoré (--skip-certbot)"
fi

if [[ "$SKIP_HEALTHCHECK" != "true" ]]; then
  log "Health checks (internes VPS + externes)"
  run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'docker exec lama-server curl -fsS http://127.0.0.1:5000/health >/dev/null'"
  run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'docker exec lama-game-webapp curl -fsS --max-time 10 http://127.0.0.1:5050/ >/dev/null'"
  run_cmd "ssh ${SSH_ARGS[*]} '$REMOTE_TARGET' 'docker exec lama-portal-webapp curl -fsS --max-time 10 http://127.0.0.1:5060/ >/dev/null'"

  # Externes: HTTPS uniquement si certbot actif et potentiellement valide.
  if [[ "$SKIP_CERTBOT" != "true" ]]; then
    run_cmd "curl -fsS --max-time 15 https://playalama.online/ >/dev/null"
    run_cmd "curl -fsS --max-time 15 https://playalama.online/live/ >/dev/null"
    run_cmd "curl -fsS --max-time 15 https://playalama.online/downloads >/dev/null"
  else
    run_cmd "curl -fsS --max-time 15 http://playalama.online/ >/dev/null"
  fi
else
  warn "Healthcheck ignoré (--skip-healthcheck)"
fi

log "Deploy terminé: tag=$DEPLOY_TAG"

