#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
ONE_ENV_SCRIPT="$ROOT_DIR/tools/scripts/db/import-lexicon.sh"

usage() {
  cat <<'EOF'
Usage:
  bash tools/scripts/db/import-lexicon-all.sh \
    --fr /path/fr.jsonl \
    --en /path/en.jsonl \
    --de /path/de.jsonl \
    --dev-connection-string "Host=...;..." \
    --staging-connection-string "Host=...;..." \
    --prod-connection-string "Host=...;..." \
    [--dry-run]

This executes the same import sequentially on: dev -> staging -> prod.
EOF
}

log() { printf '\033[0;36m[LEXICON-ALL]\033[0m %s\n' "$*"; }
err() { printf '\033[0;31m[LEXICON-ALL][ERROR]\033[0m %s\n' "$*" >&2; exit 1; }

FR_PATH=""
EN_PATH=""
DE_PATH=""
DEV_CONNECTION_STRING=""
STAGING_CONNECTION_STRING=""
PROD_CONNECTION_STRING=""
DRY_RUN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --fr) FR_PATH="${2:-}"; shift 2 ;;
    --en) EN_PATH="${2:-}"; shift 2 ;;
    --de) DE_PATH="${2:-}"; shift 2 ;;
    --dev-connection-string) DEV_CONNECTION_STRING="${2:-}"; shift 2 ;;
    --staging-connection-string) STAGING_CONNECTION_STRING="${2:-}"; shift 2 ;;
    --prod-connection-string) PROD_CONNECTION_STRING="${2:-}"; shift 2 ;;
    --dry-run) DRY_RUN=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) err "Unknown option: $1" ;;
  esac
done

[[ -n "$FR_PATH" ]] || err "--fr is required."
[[ -n "$EN_PATH" ]] || err "--en is required."
[[ -n "$DE_PATH" ]] || err "--de is required."
[[ -n "$DEV_CONNECTION_STRING" ]] || err "--dev-connection-string is required."
[[ -n "$STAGING_CONNECTION_STRING" ]] || err "--staging-connection-string is required."
[[ -n "$PROD_CONNECTION_STRING" ]] || err "--prod-connection-string is required."

for f in "$FR_PATH" "$EN_PATH" "$DE_PATH"; do
  [[ -f "$f" ]] || err "Input file not found: $f"
done

run_env() {
  local env_name="$1"
  local conn="$2"
  local -a extra_args=()
  if [[ "$DRY_RUN" == "true" ]]; then
    extra_args+=(--dry-run)
  fi

  log "Running env=$env_name"
  bash "$ONE_ENV_SCRIPT" \
    --env "$env_name" \
    --connection-string "$conn" \
    --fr "$FR_PATH" \
    --en "$EN_PATH" \
    --de "$DE_PATH" \
    "${extra_args[@]}"
}

run_env dev "$DEV_CONNECTION_STRING"
run_env staging "$STAGING_CONNECTION_STRING"
run_env prod "$PROD_CONNECTION_STRING"

log "All environments completed successfully."
