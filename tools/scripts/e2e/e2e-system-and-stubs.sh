#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SESSION_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "$SESSION_DIR"
}
trap cleanup EXIT

export LAMA_SESSION_DIR="$SESSION_DIR"

run_lama() {
  dotnet run --project "$ROOT_DIR/src/apps/Lama.Console/Lama.Console.csproj" -- "$@"
}

assert_contains() {
  local haystack="$1"
  local needle="$2"
  local label="$3"

  if [[ "$haystack" != *"$needle"* ]]; then
    echo "[E2E][ERREUR] $label : sortie attendue absente -> '$needle'" >&2
    echo "$haystack" >&2
    exit 1
  fi
}

cat > "$SESSION_DIR/session.json" <<'JSON'
{
  "gameId": null,
  "playerId": null,
  "playerName": "AdminLocal",
  "role": "admin",
  "gameLevel": null,
  "authToken": null,
  "tokenExpiresAt": null,
  "createdAt": "2026-06-18T00:00:00Z",
  "updatedAt": "2026-06-18T00:00:00Z"
}
JSON

echo "[E2E] Scenario commandes system/player/tournament"

out_status="$(run_lama system status --output json)"
assert_contains "$out_status" "isInitialized" "system.status"

out_restart="$(run_lama system restart)"
assert_contains "$out_restart" "Redémarrage logique terminé" "system.restart"

out_player="$(run_lama player create Carla)"
assert_contains "$out_player" "Profil joueur créé" "player.create"

out_tournament="$(run_lama tournament create OpenLocal)"
assert_contains "$out_tournament" "Tournoi créé" "tournament.create"

echo "[E2E] OK - commandes stubs implémentees"

