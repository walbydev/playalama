#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SOLUTION_FILE="$ROOT_DIR/Lama.slnx"
SERVER_PROJECT="$ROOT_DIR/src/apps/Lama.Server/Lama.Server.csproj"
STAGE_DIR="$ROOT_DIR/.deploy/stage"

REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
REMOTE_BASE_DIR="${LAMA_DEPLOY_REMOTE_BASE:-/opt/playalama}"
REMOTE_APP_DIR="$REMOTE_BASE_DIR/lamaserver"
REMOTE_COMPOSE_FILE="$REMOTE_BASE_DIR/docker-compose.yml"

HEALTHCHECK_URL="${LAMA_DEPLOY_HEALTHCHECK_URL:-https://playalama.online/health}"

RUN_TESTS=false
SKIP_BUILD=false
SKIP_HEALTHCHECK=false
DRY_RUN=false
SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"

usage() {
  cat <<'EOF'
Usage: tools/scripts/deploy-vps.sh [options]

Build + publish Lama.Server, copy artifacts to VPS, rebuild and redeploy Docker service.

Options:
  --target <user@host>         SSH target (default: debian@playalama.online)
  --remote-base <path>         Remote base dir (default: /opt/playalama)
  --ssh-key <file>             SSH private key path
  --run-tests                  Run dotnet test before publish
  --skip-build                 Skip dotnet build
  --skip-healthcheck           Skip final health check
  --dry-run                    Print commands without executing remote changes
  -h, --help                   Show this help

Environment overrides:
  LAMA_DEPLOY_TARGET
  LAMA_DEPLOY_REMOTE_BASE
  LAMA_DEPLOY_HEALTHCHECK_URL
  LAMA_DEPLOY_SSH_KEY
EOF
}

log() {
  printf '[DEPLOY] %s\n' "$*"
}

run_cmd() {
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '[DRY-RUN] %s\n' "$*"
    return 0
  fi

  eval "$*"
}

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[DEPLOY][ERREUR] Commande manquante: $cmd" >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      REMOTE_TARGET="$2"
      shift 2
      ;;
    --remote-base)
      REMOTE_BASE_DIR="$2"
      REMOTE_APP_DIR="$REMOTE_BASE_DIR/lamaserver"
      REMOTE_COMPOSE_FILE="$REMOTE_BASE_DIR/docker-compose.yml"
      shift 2
      ;;
    --ssh-key)
      SSH_KEY_FILE="$2"
      shift 2
      ;;
    --run-tests)
      RUN_TESTS=true
      shift
      ;;
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --skip-healthcheck)
      SKIP_HEALTHCHECK=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "[DEPLOY][ERREUR] Option inconnue: $1" >&2
      usage
      exit 1
      ;;
  esac
done

require_cmd dotnet
require_cmd ssh
require_cmd curl

SSH_ARGS=()
if [[ -n "$SSH_KEY_FILE" ]]; then
  SSH_ARGS+=("-i" "$SSH_KEY_FILE")
fi
SSH_ARGS+=("-o" "BatchMode=yes" "-o" "StrictHostKeyChecking=accept-new")

remote_has_command() {
  local cmd="$1"
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "command -v '$cmd' >/dev/null 2>&1"
}

sync_dir() {
  local local_dir="$1"
  local remote_dir="$2"
  local label="$3"

  log "Sync ${label}"

  if [[ "$DRY_RUN" == "true" ]]; then
    printf '[DRY-RUN] sync %q -> %q\n' "$local_dir" "$REMOTE_TARGET:$remote_dir"
    return 0
  fi

  if command -v rsync >/dev/null 2>&1 && remote_has_command rsync; then
    rsync -az --delete -e "ssh ${SSH_ARGS[*]}" "$local_dir/" "$REMOTE_TARGET:$remote_dir/"
    return 0
  fi

  log "rsync indisponible (local ou VPS), fallback tar+ssh"
  tar -C "$local_dir" -cf - . | ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "mkdir -p '$remote_dir' && rm -rf '$remote_dir'/* && tar -xf - -C '$remote_dir'"
}

if [[ "$SKIP_BUILD" != "true" ]]; then
  if [[ -f "$SOLUTION_FILE" ]]; then
    log "Build solution: $SOLUTION_FILE"
    dotnet build "$SOLUTION_FILE" -c Release
  else
    log "Build fallback on server project"
    dotnet build "$SERVER_PROJECT" -c Release
  fi
fi

if [[ "$RUN_TESTS" == "true" ]]; then
  log "Run tests"
  dotnet test "$ROOT_DIR" -c Release --no-build
fi

log "Publish server"
rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR/publish"

dotnet publish "$SERVER_PROJECT" -c Release -o "$STAGE_DIR/publish" --nologo

# Keep Dockerfile compatibility on VPS where Dockerfile copies both publish/ and assets/.
if [[ -d "$STAGE_DIR/publish/assets" ]]; then
  mkdir -p "$STAGE_DIR/assets"
  rm -rf "$STAGE_DIR/assets"
  cp -a "$STAGE_DIR/publish/assets" "$STAGE_DIR/assets"
fi

log "Prepare remote directories"
if [[ "$DRY_RUN" == "true" ]]; then
  printf '[DRY-RUN] ssh %s %q\n' "$REMOTE_TARGET" "mkdir -p '$REMOTE_APP_DIR/publish' '$REMOTE_APP_DIR/assets'"
else
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "mkdir -p '$REMOTE_APP_DIR/publish' '$REMOTE_APP_DIR/assets'"
fi

sync_dir "$STAGE_DIR/publish" "$REMOTE_APP_DIR/publish" "publish files"

if [[ -d "$STAGE_DIR/assets" ]]; then
  sync_dir "$STAGE_DIR/assets" "$REMOTE_APP_DIR/assets" "assets files"
fi

log "Rebuild and restart gameserver on VPS"
REMOTE_DEPLOY_CMD="cd '$REMOTE_BASE_DIR' && docker compose -f '$REMOTE_COMPOSE_FILE' build gameserver && docker compose -f '$REMOTE_COMPOSE_FILE' up -d gameserver"
if [[ "$DRY_RUN" == "true" ]]; then
  printf '[DRY-RUN] ssh %s %q\n' "$REMOTE_TARGET" "$REMOTE_DEPLOY_CMD"
else
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "$REMOTE_DEPLOY_CMD"
fi

if [[ "$SKIP_HEALTHCHECK" != "true" ]]; then
  log "Health check: $HEALTHCHECK_URL"
  curl -fsS "$HEALTHCHECK_URL" >/dev/null
fi

log "Deploy termine"

