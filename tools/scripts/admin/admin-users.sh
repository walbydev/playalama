#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=tools/scripts/admin/lib/common.sh
source "${SCRIPT_DIR}/lib/common.sh"

usage() {
  cat <<'EOF'
Usage:
  bash tools/scripts/admin/admin-users.sh [global options] <action> [action options]

Global options:
  --env dev|staging|prod
  --server-url <url>
  --admin-secret <secret>
  --token <jwt>              (ou variable LAMA_ADMIN_TOKEN)
  --json
  --dry-run

Actions:
  list
  register --username <u> --password <p> [--email <e>] [--country <cc>]
  login --username <u> --password <p>
  status
  profile
  update-profile [--email <e>] [--country <cc>] [--new-password <p>] [--current-password <p>]
EOF
}

require_value() {
  local option="$1"
  local value="${2:-}"
  [[ -n "${value}" ]] || log_error "Option ${option} requise."
}

json_or_null() {
  local value="${1:-}"
  if [[ -z "${value}" ]]; then
    printf 'null'
  else
    json_escape "${value}"
  fi
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

username=""
password=""
email=""
country=""
new_password=""
current_password=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --username)
      [[ $# -ge 2 ]] || log_error "Option --username sans valeur."
      username="$2"
      shift 2
      ;;
    --password)
      [[ $# -ge 2 ]] || log_error "Option --password sans valeur."
      password="$2"
      shift 2
      ;;
    --email)
      [[ $# -ge 2 ]] || log_error "Option --email sans valeur."
      email="$2"
      shift 2
      ;;
    --country)
      [[ $# -ge 2 ]] || log_error "Option --country sans valeur."
      country="$2"
      shift 2
      ;;
    --new-password)
      [[ $# -ge 2 ]] || log_error "Option --new-password sans valeur."
      new_password="$2"
      shift 2
      ;;
    --current-password)
      [[ $# -ge 2 ]] || log_error "Option --current-password sans valeur."
      current_password="$2"
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
    api_call GET "/api/v1/admin/users" | print_json_or_raw
    ;;
  register)
    require_value "--username" "${username}"
    require_value "--password" "${password}"
    payload="{\"username\":$(json_escape "${username}"),\"password\":$(json_escape "${password}"),\"email\":$(json_or_null "${email}"),\"countryCode\":$(json_or_null "${country}")}"
    api_call POST "/api/v1/auth/register" "${payload}" | print_json_or_raw
    ;;
  login)
    require_value "--username" "${username}"
    require_value "--password" "${password}"
    payload="{\"username\":$(json_escape "${username}"),\"password\":$(json_escape "${password}")}"
    api_call POST "/api/v1/auth/login/account" "${payload}" | print_json_or_raw
    ;;
  status)
    api_call GET "/api/v1/auth/status" | print_json_or_raw
    ;;
  profile)
    api_call GET "/api/v1/players/me" | print_json_or_raw
    ;;
  update-profile)
    payload="{\"email\":$(json_or_null "${email}"),\"newPassword\":$(json_or_null "${new_password}"),\"currentPassword\":$(json_or_null "${current_password}"),\"countryCode\":$(json_or_null "${country}")}"
    api_call PUT "/api/v1/players/me" "${payload}" | print_json_or_raw
    ;;
  *)
    log_error "Action inconnue '${action}'."
    ;;
esac
