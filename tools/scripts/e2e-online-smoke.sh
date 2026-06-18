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
SERVER_URL=""
STARTED_LOCAL_SERVER=false

cleanup() {
  if [[ "$STARTED_LOCAL_SERVER" == "true" && -n "$SERVER_URL" ]]; then
    curl -fsS -X POST "${SERVER_URL}/internal/shutdown" >/dev/null 2>&1 || true
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
  LAMA_SERVER_URL="$SERVER_URL" \
  LAMA_SESSION_DIR="$session_dir" \
  dotnet run --project "$CONSOLE_PROJECT" -- "$@"
}

extract_json_value() {
  local json_payload="$1"
  local python_expr="$2"

  JSON_PAYLOAD="$json_payload" PYTHON_EXPR="$python_expr" python - <<'PY'
import json
import os

payload = json.loads(os.environ["JSON_PAYLOAD"])
expr = os.environ["PYTHON_EXPR"]
result = eval(expr, {"__builtins__": {}}, {"data": payload})
print(result)
PY
}

assert_python_json() {
  local json_payload="$1"
  local label="$2"
  local python_code="$3"

  if ! JSON_PAYLOAD="$json_payload" python - <<PY
import json
import os
import sys

data = json.loads(os.environ["JSON_PAYLOAD"])

$python_code
PY
  then
    echo "[E2E-ONLINE][ERREUR] $label" >&2
    echo "$json_payload" >&2
    exit 1
  fi
}

SERVER_URL="${LAMA_E2E_SERVER_URL:-}"
SERVER_URL="${SERVER_URL%/}"

if [[ -z "$SERVER_URL" ]]; then
  SERVER_PORT="$(find_free_port)"
  if [[ -z "$SERVER_PORT" ]]; then
    echo "[E2E-ONLINE][ERREUR] Aucun port libre trouve entre 5100 et 5199" >&2
    exit 1
  fi

  SERVER_URL="http://127.0.0.1:${SERVER_PORT}"
  echo "[E2E-ONLINE] Demarrage serveur local sur ${SERVER_URL}"
  LAMA_SERVER_ALLOW_SHUTDOWN=true \
    dotnet run --project "$SERVER_PROJECT" --urls "$SERVER_URL" >| "$LOG_FILE" 2>&1 &
  SERVER_PID=$!
  STARTED_LOCAL_SERVER=true
else
  echo "[E2E-ONLINE] Utilisation du serveur distant ${SERVER_URL}"
fi

for _ in $(seq 1 40); do
  if curl -fsS "${SERVER_URL}/health" >/dev/null 2>&1; then
    break
  fi
  sleep 0.2
done

if ! curl -fsS "${SERVER_URL}/health" >/dev/null 2>&1; then
  echo "[E2E-ONLINE][ERREUR] Le serveur n'a pas repondu a /health" >&2
  if [[ "$STARTED_LOCAL_SERVER" == "true" ]]; then
    tail -40 "$LOG_FILE" >&2 || true
  fi
  exit 1
fi

echo "[E2E-ONLINE] Scenario: host create -> guest join -> host move -> show.board/rack/scores/history -> host end"

playable_word=""
game_id=""

for attempt in $(seq 1 5); do
  out_create="$(run_lama_with_session "$SESSION_DIR_HOST" game create Alice --level casual)"
  assert_contains "$out_create" "Partie créée" "game.create"
  assert_contains "$out_create" "Mode      : online" "game.create mode"

  game_id="$(echo "$out_create" | sed -n 's/.*ID : \([a-f0-9]\{32\}\).*/\1/p' | head -n 1)"
  if [[ -z "$game_id" ]]; then
    echo "[E2E-ONLINE][ERREUR] Impossible d'extraire le game id depuis game.create" >&2
    exit 1
  fi

  out_join="$(run_lama_with_session "$SESSION_DIR_GUEST" game join Bob --game-id "$game_id")"
  assert_contains "$out_join" "rejoint la partie online" "game.join online"

  host_snapshot="$(run_lama_with_session "$SESSION_DIR_HOST" game show "$game_id" --output json)"
  rack_letters="$(extract_json_value "$host_snapshot" "''.join(ch for ch in data['Players'][0]['Rack'] if ch.isalpha())")"

  if [[ -z "$rack_letters" ]]; then
    continue
  fi

  anagrams_json="$(run_lama_with_session "$SESSION_DIR_HOST" dict anagram "$rack_letters" --min-length 2 --output json)"
  playable_word="$(extract_json_value "$anagrams_json" "data[0] if data else ''")"

  if [[ -n "$playable_word" ]]; then
    break
  fi
done

if [[ -z "$playable_word" ]]; then
  echo "[E2E-ONLINE][ERREUR] Aucun mot jouable trouve apres 5 tentatives de creation" >&2
  exit 1
fi

echo "[E2E-ONLINE] Partie retenue: $game_id | mot joue: $playable_word"

out_move_host="$(run_lama_with_session "$SESSION_DIR_HOST" play move H8 "$playable_word" H)"
assert_contains "$out_move_host" "(online)" "play.move online"
assert_contains "$out_move_host" "MoveId" "play.move moveId"

snapshot_after_move="$(run_lama_with_session "$SESSION_DIR_HOST" game show "$game_id" --output json)"
PLAYABLE_WORD="$playable_word" assert_python_json "$snapshot_after_move" "game.show snapshot after play.move" '
import os
word = os.environ.get("PLAYABLE_WORD", "")
players = data["Players"]
board = data["Board"]
moves = data["Moves"]

assert data["CurrentPlayerIndex"] == 1, "le tour devrait passer au joueur 2"
assert len(board) >= len(word), "le plateau ne contient pas assez de tuiles"
assert players[0]["Score"] > 0, "le score du joueur 1 devrait etre strictement positif"
assert len(moves) == 1, "l historique online devrait contenir exactement un coup"

move = moves[0]
assert move["Command"] == "play.move", "la commande historisee devrait etre play.move"
assert move.get("TurnNumber", 0) == 1, "le numero de tour attendu est 1"

placements = move.get("Placements") or []
assert len(placements) == len(word), "le nombre de placements historises est incorrect"

expected = {(7, 7 + i, ch.upper()) for i, ch in enumerate(word)}
actual = {(p["Row"], p["Column"], p["Letter"]) for p in placements}
missing = expected - actual
assert not missing, f"placements manquants: {sorted(missing)}"
' 

out_board="$(run_lama_with_session "$SESSION_DIR_HOST" show board --no-color)"
assert_contains "$out_board" "A" "show.board headers"
assert_contains "$out_board" "8" "show.board row label"

out_rack="$(run_lama_with_session "$SESSION_DIR_HOST" show rack)"
assert_contains "$out_rack" "Rack de Alice" "show.rack player"
assert_contains "$out_rack" "7 lettre(s)" "show.rack count"

out_scores="$(run_lama_with_session "$SESSION_DIR_HOST" show scores --output json)"
assert_contains "$out_scores" "\"name\"" "show.scores json names"
assert_python_json "$out_scores" "show.scores score update" '
alice = next((player for player in data if player["name"] == "Alice"), None)
assert alice is not None, "Alice absente des scores"
assert alice["score"] > 0, "score Alice non mis a jour"
'

out_history_text="$(run_lama_with_session "$SESSION_DIR_HOST" show history)"
assert_contains "$out_history_text" "Tour 1" "show.history text turn"
assert_contains "$out_history_text" "H8:" "show.history text placements"

out_history_json="$(run_lama_with_session "$SESSION_DIR_HOST" show history --output json)"
assert_python_json "$out_history_json" "show.history json detail" '
assert isinstance(data, list) and len(data) == 1, "historique JSON invalide"
item = data[0]
assert item["TurnNumber"] == 1, "turnNumber JSON incorrect"
assert item["PlayerName"] == "Alice", "playerName JSON incorrect"
assert "H8:" in item["Placements"], "placements JSON absents"
assert item["Score"] > 0, "score JSON non mis a jour"
'

out_end="$(run_lama_with_session "$SESSION_DIR_HOST" game end)"
assert_contains "$out_end" "PARTIE TERMINÉE" "game.end"

echo "[E2E-ONLINE] OK - parcours online valide"

