#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SCHEMA_SQL="$ROOT_DIR/tools/postgres/05-init-lexicon-schema.sql"
IMPORTER_PROJECT="$ROOT_DIR/tools/importers/Lama.DictionaryImporter/Lama.DictionaryImporter.csproj"

usage() {
  cat <<'EOF'
Usage:
  bash tools/scripts/db/import-lexicon.sh \
    --env dev|staging|prod \
    --connection-string "Host=...;Port=5432;Database=...;Username=...;Password=...;" \
    --fr /path/fr.jsonl \
    --en /path/en.jsonl \
    --de /path/de.jsonl \
    [--keep-language-data] \
    [--dry-run]

Notes:
  - The script applies lexicon schema SQL, then imports fr/en/de in order.
  - Import includes words, definitions, synonyms, and Wiktionary URL if present.
  - Import is idempotent through lexicon.import_runs fingerprint checks.
EOF
}

log() { printf '\033[0;36m[LEXICON]\033[0m %s\n' "$*"; }
err() { printf '\033[0;31m[LEXICON][ERROR]\033[0m %s\n' "$*" >&2; exit 1; }

ENV_NAME=""
CONNECTION_STRING=""
FR_PATH=""
EN_PATH=""
DE_PATH=""
DRY_RUN=false
KEEP_LANGUAGE_DATA=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENV_NAME="${2:-}"; shift 2 ;;
    --connection-string) CONNECTION_STRING="${2:-}"; shift 2 ;;
    --fr) FR_PATH="${2:-}"; shift 2 ;;
    --en) EN_PATH="${2:-}"; shift 2 ;;
    --de) DE_PATH="${2:-}"; shift 2 ;;
    --keep-language-data) KEEP_LANGUAGE_DATA=true; shift ;;
    --dry-run) DRY_RUN=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) err "Unknown option: $1" ;;
  esac
done

[[ -z "$ENV_NAME" ]] && err "--env is required."
[[ -z "$CONNECTION_STRING" ]] && err "--connection-string is required."
[[ -z "$FR_PATH" ]] && err "--fr is required."
[[ -z "$EN_PATH" ]] && err "--en is required."
[[ -z "$DE_PATH" ]] && err "--de is required."

case "$ENV_NAME" in
  dev|staging|prod) ;;
  *) err "--env must be one of: dev, staging, prod." ;;
esac

for f in "$FR_PATH" "$EN_PATH" "$DE_PATH"; do
  [[ -f "$f" ]] || err "Input file not found: $f"
done

command -v psql >/dev/null 2>&1 || err "psql is required and not found."
command -v dotnet >/dev/null 2>&1 || err "dotnet is required and not found."

log "Applying lexicon schema..."
psql "$CONNECTION_STRING" -v ON_ERROR_STOP=1 -f "$SCHEMA_SQL"

run_import() {
  local lang="$1"
  local file="$2"
  local -a extra_args=()
  if [[ "$DRY_RUN" == "true" ]]; then
    extra_args+=(--dry-run)
  fi
  if [[ "$KEEP_LANGUAGE_DATA" == "true" ]]; then
    extra_args+=(--keep-language-data)
  fi
  log "Importing language=$lang file=$file"
  dotnet run --project "$IMPORTER_PROJECT" -- \
    --connection-string "$CONNECTION_STRING" \
    --environment "$ENV_NAME" \
    --language "$lang" \
    --input "$file" \
    --include-definitions \
    --include-synonyms \
    --batch-size 5000 \
    "${extra_args[@]}"
}

run_import fr "$FR_PATH"
run_import en "$EN_PATH"
run_import de "$DE_PATH"

if [[ "$DRY_RUN" == "false" ]]; then
  log "Post-import counts:"
  psql "$CONNECTION_STRING" -v ON_ERROR_STOP=1 <<'SQL'
SELECT language_code, COUNT(*) AS words
FROM lexicon.words
GROUP BY language_code
ORDER BY language_code;

SELECT language_code, status, words_count, definitions_count, synonyms_count, completed_at
FROM lexicon.import_runs
ORDER BY started_at DESC
LIMIT 9;
SQL
fi

log "Lexicon import finished."
