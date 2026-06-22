#!/usr/bin/env bash
set -euo pipefail

SERVER_URL="${SERVER_URL:-http://127.0.0.1:5201}"
WEBAPP_URL="${WEBAPP_URL:-http://127.0.0.1:5202}"

log() {
  printf '[web-lobby-smoke] %s\n' "$1"
}

json_get() {
  local payload="$1"
  local expr="$2"
  python3 - <<'PY' "$payload" "$expr"
import json
import sys
payload = json.loads(sys.argv[1])
expr = sys.argv[2].split('.')
cur = payload
for key in expr:
    if key:
        cur = cur[key]
if isinstance(cur, bool):
    print('true' if cur else 'false')
else:
    print(cur)
PY
}

log "Vérification accessibilité WebApp: ${WEBAPP_URL}"
curl -fsS "${WEBAPP_URL}/" >/dev/null

log "Vérification accessibilité Server: ${SERVER_URL}"
curl -fsS "${SERVER_URL}/health" >/dev/null

suffix="$(date +%s)"
username="web_smoke_${suffix}"
password="SmokePass123!"
email="${username}@example.com"

log "Inscription utilisateur test: ${username}"
register_response="$(curl -fsS -X POST "${SERVER_URL}/api/v1/auth/register" \
  -H 'Content-Type: application/json' \
  -d "{\"username\":\"${username}\",\"password\":\"${password}\",\"email\":\"${email}\"}")"

token="$(json_get "$register_response" token)"
player_id="$(json_get "$register_response" playerId)"

if [[ -z "$token" || -z "$player_id" ]]; then
  log "ERREUR: token/playerId manquant dans la réponse register"
  exit 1
fi

game_name="smoke-lobby-${suffix}"
log "Création d'une partie lobby: ${game_name}"
create_response="$(curl -fsS -X POST "${SERVER_URL}/api/v1/games" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer ${token}" \
  -d "{\"hostName\":\"${username}\",\"mode\":1,\"gameName\":\"${game_name}\",\"maxPlayers\":2,\"enableAi\":true,\"language\":\"fr\"}")"

game_id="$(json_get "$create_response" gameId)"

if [[ -z "$game_id" ]]; then
  log "ERREUR: gameId manquant dans createGame"
  exit 1
fi

log "Lecture snapshot avant démarrage"
snapshot_before="$(curl -fsS "${SERVER_URL}/api/v1/games/${game_id}")"
uses_lobby="$(json_get "$snapshot_before" usesLobby)"
has_started_before="$(json_get "$snapshot_before" hasStarted)"

if [[ "$uses_lobby" != "true" ]]; then
  log "ERREUR: usesLobby attendu=true, obtenu=${uses_lobby}"
  exit 1
fi

if [[ "$has_started_before" != "false" ]]; then
  log "ERREUR: hasStarted attendu=false avant start, obtenu=${has_started_before}"
  exit 1
fi

log "Démarrage de la partie par l'hôte"
curl -fsS -X POST "${SERVER_URL}/api/v1/games/${game_id}/start" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer ${token}" \
  -d '{}' >/dev/null

log "Lecture snapshot après démarrage"
snapshot_after="$(curl -fsS "${SERVER_URL}/api/v1/games/${game_id}")"
has_started_after="$(json_get "$snapshot_after" hasStarted)"

if [[ "$has_started_after" != "true" ]]; then
  log "ERREUR: hasStarted attendu=true après start, obtenu=${has_started_after}"
  exit 1
fi

log "Validation endpoint Mes parties"
my_games_status="$(curl -s -o /tmp/lama_my_games_smoke.json -w '%{http_code}' \
  -H "Authorization: Bearer ${token}" \
  "${SERVER_URL}/api/v1/players/me/games")"

if [[ "$my_games_status" != "200" ]]; then
  log "ERREUR: /players/me/games retourne HTTP ${my_games_status}"
  cat /tmp/lama_my_games_smoke.json
  exit 1
fi

log "Validation structure JSON games (queue string/number toléré)"
list_games_response="$(curl -fsS "${SERVER_URL}/api/v1/games")"
python3 - <<'PY' "$list_games_response"
import json
import sys
payload = json.loads(sys.argv[1])
games = payload.get('games', [])
if not isinstance(games, list):
    raise SystemExit('games n\'est pas une liste')
for g in games:
    q = g.get('queue')
    if not isinstance(q, (str, int)):
        raise SystemExit(f'queue type inattendu: {type(q)}')
print('ok')
PY

log "SUCCÈS: smoke test web lobby validé"
log "gameId=${game_id} hostPlayerId=${player_id}"

