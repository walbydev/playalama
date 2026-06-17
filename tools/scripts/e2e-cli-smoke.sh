#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SESSION_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "$SESSION_DIR"
}
trap cleanup EXIT

export LAMA_SESSION_DIR="$SESSION_DIR"

run_lama() {
  dotnet run --project "$ROOT_DIR/src/Console/Lama.Console/Lama.Console.csproj" -- "$@"
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

echo "[E2E] Scenario CLI reel: create -> join -> pass -> show -> end"

out_create="$(run_lama game create Alice)"
assert_contains "$out_create" "Partie créée" "game.create"

game_id="$(echo "$out_create" | sed -n 's/.*ID : \([a-f0-9]\{32\}\).*/\1/p' | head -n 1)"
if [[ -z "$game_id" ]]; then
  echo "[E2E][ERREUR] Impossible d'extraire le game id depuis game.create" >&2
  exit 1
fi

out_join="$(run_lama game join Bob)"
assert_contains "$out_join" "a rejoint la partie" "game.join"

out_pass="$(run_lama play pass)"
assert_contains "$out_pass" "Tour passé" "play.pass"

out_show="$(run_lama game show --output json)"
assert_contains "$out_show" "$game_id" "game.show --output json"

out_status="$(run_lama show scores)"
assert_contains "$out_status" "Scores" "show.scores"

out_end="$(run_lama game end)"
assert_contains "$out_end" "PARTIE TERMINÉE" "game.end"

echo "[E2E] OK - parcours complet valide"

