#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SERVER_PROJECT="$ROOT_DIR/src/Server/Lama.Server/Lama.Server.csproj"
CONSOLE_PROJECT="$ROOT_DIR/src/Console/Lama.Console/Lama.Console.csproj"

SESSION_DIR_HOST="$(mktemp -d)"
SESSION_DIR_GUEST="$(mktemp -d)"
LOG_FILE="$(mktemp)"
SERVER_PID=""
SERVER_PORT=""

cleanup() {
  if [[ -n "$SERVER_PORT" ]]; then
    curl -fsS -X POST "http://127.0.0.1:${SERVER_PORT}/internal/shutdown" >/dev/null 2>&1 || true
  fi

  if [[ -n "$SERVER_PID" ]]; then
    wait "$SERVER_PID" 2>/dev/null || true
  fi

  rm -rf "$SESSION_DIR_HOST" "$SESSION_DIR_GUEST"
  rm -f "$LOG_FILE"
}
trap cleanup EXIT

assert_contains() {
  local haystack="$1"
  local needle="$2"
  local label="$3"

  if [[ "$haystack" != *"$needle"* ]]; then
    echo "[E2E-ONLINE][ERREUR] $label : sortie attendue absente -> '$needle'" >&2
    echo "$haystack" >&2
    exit 1
  fi
}

find_free_port() {
  local p
  for p in $(seq 5100 5199); do
    if ! ss -ltn | awk '{print $4}' | grep -q ":${p}$"; then
      echo "$p"
      return 0
    fi
  done

  return 1
}

run_lama_with_session() {
  local session_dir="$1"
  shift

  LAMA_RUNTIME_MODE=online \
  LAMA_SERVER_URL="http://127.0.0.1:${SERVER_PORT}" \
  LAMA_SESSION_DIR="$session_dir" \
  dotnet run --project "$CONSOLE_PROJECT" -- "$@"
}

SERVER_PORT="$(find_free_port)"
if [[ -z "$SERVER_PORT" ]]; then
  echo "[E2E-ONLINE][ERREUR] Aucun port libre trouve entre 5100 et 5199" >&2
  exit 1
fi

echo "[E2E-ONLINE] Demarrage serveur sur port ${SERVER_PORT}"
LAMA_SERVER_ALLOW_SHUTDOWN=true \
  dotnet run --project "$SERVER_PROJECT" --urls "http://127.0.0.1:${SERVER_PORT}" >| "$LOG_FILE" 2>&1 &
SERVER_PID=$!

for _ in $(seq 1 40); do
  if curl -fsS "http://127.0.0.1:${SERVER_PORT}/health" >/dev/null 2>&1; then
    break
  fi
  sleep 0.2
done

if ! curl -fsS "http://127.0.0.1:${SERVER_PORT}/health" >/dev/null 2>&1; then
  echo "[E2E-ONLINE][ERREUR] Le serveur n'a pas repondu a /health" >&2
  tail -40 "$LOG_FILE" >&2 || true
  exit 1
fi

echo "[E2E-ONLINE] Scenario: host create -> guest join -> host show -> host end"

out_create="$(run_lama_with_session "$SESSION_DIR_HOST" game create Alice --level standard)"
assert_contains "$out_create" "Partie créée" "game.create"
assert_contains "$out_create" "Mode      : online" "game.create mode"

game_id="$(echo "$out_create" | sed -n 's/.*ID : \([a-f0-9]\{32\}\).*/\1/p' | head -n 1)"
if [[ -z "$game_id" ]]; then
  echo "[E2E-ONLINE][ERREUR] Impossible d'extraire le game id depuis game.create" >&2
  exit 1
fi

out_join="$(run_lama_with_session "$SESSION_DIR_GUEST" game join Bob --game-id "$game_id")"
assert_contains "$out_join" "rejoint la partie online" "game.join online"

out_show="$(run_lama_with_session "$SESSION_DIR_HOST" game show "$game_id" --output json)"
assert_contains "$out_show" "$game_id" "game.show json"
assert_contains "$out_show" "Bob" "game.show players"

out_end="$(run_lama_with_session "$SESSION_DIR_HOST" game end)"
assert_contains "$out_end" "PARTIE TERMINÉE" "game.end"

echo "[E2E-ONLINE] OK - parcours online valide"

