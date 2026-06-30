#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=tools/scripts/admin/lib/common.sh
source "${SCRIPT_DIR}/lib/common.sh"

usage() {
  cat <<'EOF'
Usage:
  bash tools/scripts/admin/admin-env.sh [--env dev|staging|prod] [--server-url <url>] [--json]

Description:
  Affiche la résolution effective des paramètres d'administration.
EOF
}

parse_common_flags "$@"
set -- "${COMMON_REMAINING_ARGS[@]}"

if [[ $# -gt 0 ]] && [[ "$1" =~ ^(-h|--help)$ ]]; then
  usage
  exit 0
fi

resolve_server_url

payload=$(
  cat <<EOF
{"env":$(json_escape "${ADMIN_ENV}"),"serverUrl":$(json_escape "${ADMIN_SERVER_URL%/}"),"hasAdminSecret":$([[ -n "${ADMIN_SECRET}" ]] && echo true || echo false),"hasBearerToken":$([[ -n "${ADMIN_BEARER_TOKEN}" ]] && echo true || echo false),"dryRun":${ADMIN_DRY_RUN}}
EOF
)

printf '%s\n' "${payload}" | print_json_or_raw
