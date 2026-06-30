#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=tools/scripts/admin/lib/common.sh
source "${SCRIPT_DIR}/lib/common.sh"

usage() {
  cat <<'EOF'
Usage:
  bash tools/scripts/admin/admin-reset.sh [global options] <action> [action options]

Global options:
  --env dev|staging|prod
  --server-url <url>
  --admin-secret <secret>
  --token <jwt>
  --json
  --dry-run
  --yes

Actions:
  reset-games
      dev: purge sessions DB + mémoire serveur.
      staging/prod: terminate-all via API admin.
  reset-users
      dev uniquement: purge rating.players (+ rating.player_ratings cascade).
  reset-stats
      dev uniquement: purge history.completed_games + rating.player_ratings.
  reset-all
      dev uniquement: reset-games + reset-stats + reset-users.
  ensure-root
      dev uniquement: garantit le compte joueur Web root/root.
EOF
}

yes=false
action=""

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

while [[ $# -gt 0 ]]; do
  case "$1" in
    --yes)
      yes=true
      shift
      ;;
    *)
      log_error "Option inconnue: $1"
      ;;
  esac
done

resolve_server_url

reset_games_dev() {
  run_dev_sql "DELETE FROM sessions.turn_log;"
  run_dev_sql "DELETE FROM sessions.players_in_game;"
  run_dev_sql "DELETE FROM sessions.board_state;"
  run_dev_sql "DELETE FROM sessions.games;"
  api_call POST "/internal/games/clear" "{}" | print_json_or_raw
}

reset_games_non_dev() {
  api_call POST "/api/v1/admin/games/terminate-all" "{}" | print_json_or_raw
}

reset_users_dev() {
  run_dev_sql "DELETE FROM rating.players;"
  if [[ "${ADMIN_JSON}" == "true" ]]; then
    printf '{"status":"ok","action":"reset-users","env":"dev"}\n'
  else
    printf 'OK: joueurs supprimés (dev).\n'
  fi
}

reset_stats_dev() {
  run_dev_sql "DELETE FROM history.completed_games;"
  run_dev_sql "DELETE FROM rating.player_ratings;"
  if [[ "${ADMIN_JSON}" == "true" ]]; then
    printf '{"status":"ok","action":"reset-stats","env":"dev"}\n'
  else
    printf 'OK: stats supprimées (dev).\n'
  fi
}

ensure_root_dev() {
  local login_payload='{"username":"root","password":"root"}'
  local root_hash
  root_hash="$(generate_server_password_hash "root")"

  if api_call POST "/api/v1/auth/login/account" "${login_payload}" >/dev/null 2>&1; then
    :
  else
    run_dev_sql "INSERT INTO rating.players (username, email, password_hash, country_code, created_at)
      VALUES ('root', NULL, '${root_hash}', NULL, NOW())
      ON CONFLICT (username)
      DO UPDATE SET
        email = EXCLUDED.email,
        password_hash = EXCLUDED.password_hash,
        country_code = EXCLUDED.country_code;"
    api_call POST "/api/v1/auth/login/account" "${login_payload}" >/dev/null
  fi

  if [[ "${ADMIN_JSON}" == "true" ]]; then
    printf '{"status":"ok","action":"ensure-root","env":"dev","username":"root"}\n'
  else
    printf 'OK: accès root/root garanti (dev).\n'
  fi
}

case "${action}" in
  reset-games)
    require_yes "${yes}"
    if [[ "${ADMIN_ENV}" == "dev" ]]; then
      reset_games_dev
    else
      reset_games_non_dev
    fi
    ;;
  reset-users)
    require_yes "${yes}"
    require_dev_env
    reset_users_dev
    ;;
  reset-stats)
    require_yes "${yes}"
    require_dev_env
    reset_stats_dev
    ;;
  reset-all)
    require_yes "${yes}"
    require_dev_env
    reset_games_dev >/dev/null
    reset_stats_dev >/dev/null
    reset_users_dev >/dev/null
    if [[ "${ADMIN_JSON}" == "true" ]]; then
      printf '{"status":"ok","action":"reset-all","env":"dev"}\n'
    else
      printf 'OK: reset complet exécuté (dev).\n'
    fi
    ;;
  ensure-root)
    require_yes "${yes}"
    require_dev_env
    ensure_root_dev
    ;;
  *)
    log_error "Action inconnue '${action}'."
    ;;
esac
