#!/usr/bin/env bash
set -euo pipefail

ADMIN_ENV="dev"
ADMIN_SERVER_URL=""
ADMIN_SECRET="${LAMA_ADMIN_SECRET:-}"
ADMIN_JSON=false
ADMIN_DRY_RUN=false
ADMIN_BEARER_TOKEN="${LAMA_ADMIN_TOKEN:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../../../.." && pwd)"
CONSOLE_PROJECT="${ROOT_DIR}/src/apps/Lama.Console/Lama.Console.csproj"

log_info() { printf '[admin] %s\n' "$*"; }
log_warn() { printf '[admin][warn] %s\n' "$*" >&2; }
log_error() { printf '[admin][error] %s\n' "$*" >&2; exit 1; }

print_cmd() {
  local rendered=""
  local part
  for part in "$@"; do
    rendered+=$(printf ' %q' "${part}")
  done
  printf '%s\n' "${rendered# }"
}

run_cmd() {
  if [[ "${ADMIN_DRY_RUN}" == "true" ]]; then
    print_cmd "$@"
    return 0
  fi
  "$@"
}

json_escape() {
  python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"
}

print_json_or_raw() {
  if [[ "${ADMIN_JSON}" == "true" ]]; then
    cat
    return 0
  fi

  if command -v python3 >/dev/null 2>&1; then
    python3 -m json.tool 2>/dev/null || cat
  else
    cat
  fi
}

parse_common_flags() {
  local -a remaining=()
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --env)
        [[ $# -ge 2 ]] || log_error "Option --env sans valeur."
        ADMIN_ENV="$2"
        shift 2
        ;;
      --server-url)
        [[ $# -ge 2 ]] || log_error "Option --server-url sans valeur."
        ADMIN_SERVER_URL="$2"
        shift 2
        ;;
      --admin-secret)
        [[ $# -ge 2 ]] || log_error "Option --admin-secret sans valeur."
        ADMIN_SECRET="$2"
        shift 2
        ;;
      --token)
        [[ $# -ge 2 ]] || log_error "Option --token sans valeur."
        ADMIN_BEARER_TOKEN="$2"
        shift 2
        ;;
      --json)
        ADMIN_JSON=true
        shift
        ;;
      --dry-run)
        ADMIN_DRY_RUN=true
        shift
        ;;
      *)
        remaining+=("$1")
        shift
        ;;
    esac
  done
  COMMON_REMAINING_ARGS=("${remaining[@]}")
}

resolve_server_url() {
  case "${ADMIN_ENV}" in
    dev)
      : "${ADMIN_SERVER_URL:=http://127.0.0.1:5201}"
      ;;
    staging)
      : "${ADMIN_SERVER_URL:=https://staging.playalama.online}"
      ;;
    prod)
      : "${ADMIN_SERVER_URL:=https://playalama.online}"
      ;;
    *)
      log_error "--env invalide '${ADMIN_ENV}' (attendu: dev|staging|prod)."
      ;;
  esac
}

require_dev_env() {
  [[ "${ADMIN_ENV}" == "dev" ]] || log_error "Action autorisée uniquement en --env dev."
}

api_call() {
  local method="$1"
  local path="$2"
  local data="${3:-}"
  local -a cmd=(
    curl -fsS -X "${method}"
    -H "Accept: application/json"
  )

  if [[ -n "${ADMIN_SECRET}" ]]; then
    cmd+=(-H "X-Admin-Secret: ${ADMIN_SECRET}")
  fi
  if [[ -n "${ADMIN_BEARER_TOKEN}" ]]; then
    cmd+=(-H "Authorization: Bearer ${ADMIN_BEARER_TOKEN}")
  fi
  if [[ -n "${data}" ]]; then
    cmd+=(-H "Content-Type: application/json" -d "${data}")
  fi

  cmd+=("${ADMIN_SERVER_URL%/}${path}")
  run_cmd "${cmd[@]}"
}
