#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=tools/scripts/admin/lib/common.sh
source "${SCRIPT_DIR}/lib/common.sh"

usage() {
  cat <<'EOF'
Usage:
  bash tools/scripts/admin/admin-games.sh [global options] <action> [action options]

Global options:
  --env dev|staging|prod
  --server-url <url>
  --admin-secret <secret>
  --token <jwt>
  --json
  --dry-run

Actions:
  list
      Liste les parties via /api/v1/games.
  show --game-id <id>
      Affiche une partie via /api/v1/games/{id}.
  terminate-all
      Termine toutes les parties actives via /api/v1/admin/games/terminate-all.
  clear-memory
      (dev uniquement) Vide la mémoire serveur via /internal/games/clear.
  close --game-id <id>
      (dev uniquement) Force la clôture mémoire d'une partie via /internal/games/{id}/close.
EOF
}

require_value() {
  local option="$1"
  local value="${2:-}"
  [[ -n "${value}" ]] || log_error "Option ${option} requise."
}

parse_common_flags "$@"
set -- "${COMMON_REMAINING_ARGS[@]}"

[[ $# -gt 0 ]] || { usage; exit 1; }
case "${1}" in
  -h|--help)
    usage
    exit 0
    ;;
esac

action="$1"
shift

game_id=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --game-id)
      [[ $# -ge 2 ]] || log_error "Option --game-id sans valeur."
      game_id="$2"
      shift 2
      ;;
    *)
      log_error "Option inconnue: $1"
      ;;
  esac
done

resolve_server_url

case "${action}" in
  list)
    api_call GET "/api/v1/games" | print_json_or_raw
    ;;
  show)
    require_value "--game-id" "${game_id}"
    api_call GET "/api/v1/games/${game_id}" | print_json_or_raw
    ;;
  terminate-all)
    api_call POST "/api/v1/admin/games/terminate-all" "{}" | print_json_or_raw
    ;;
  clear-memory)
    require_dev_env
    api_call POST "/internal/games/clear" "{}" | print_json_or_raw
    ;;
  close)
    require_dev_env
    require_value "--game-id" "${game_id}"
    api_call POST "/internal/games/${game_id}/close" "{}" | print_json_or_raw
    ;;
  *)
    log_error "Action inconnue '${action}'."
    ;;
esac
