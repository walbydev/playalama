#!/usr/bin/env bash
# =============================================================================
# restore-lexicon-dump.sh — Restaure le schéma lexicon (mots/définitions/
# synonymes) depuis un dump de la base dev vers PROD et/ou STAGING sur le VPS.
# =============================================================================
# Stratégie : (option) pg_dump dev local → scp (clé SSH) → docker cp →
#             DROP SCHEMA lexicon CASCADE → pg_restore → restart services.
# N'affecte QUE le schéma lexicon (sessions/games/players restent intacts).
#
# Usage:
#   bash tools/scripts/db/restore-lexicon-dump.sh --env prod    --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/db/restore-lexicon-dump.sh --env staging --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/db/restore-lexicon-dump.sh --env both --create-dump --ssh-key ~/.ssh/playalama.key
#
# Options:
#   --env <prod|staging|both>   Environnement(s) cible(s) (obligatoire)
#   --dump <file>               Dump à restaurer (défaut: /tmp/lexicon.dump)
#   --create-dump               (Re)générer le dump depuis la base dev locale
#   --ssh-key <file>            Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --target <user@host>        Cible SSH (défaut: debian@playalama.online)
#   --db-user <name>            Surcharge user Postgres distant (défaut: lama_<env>)
#   --db-name <name>            Surcharge DB Postgres distante (défaut: lama_<env>)
#   --skip-restart              Ne pas redémarrer server + aiserver après restore
#   --skip-transfer             Réutiliser un dump déjà présent sur le VPS (/tmp)
#   --dry-run                   Afficher les commandes sans les exécuter
#   -h, --help                  Afficher cette aide
#
# Paramètres base dev locale (pour --create-dump) :
#   --local-container <name>    Conteneur Postgres dev  (défaut: postgres-lama-debug)
#   --local-db <name>           DB dev                  (défaut: lama_dev)
#   --local-user <name>         User dev                (défaut: lama_dev)
#
# Variables d'environnement :
#   LAMA_DEPLOY_SSH_KEY         Chemin vers la clé SSH
#   LAMA_DEPLOY_TARGET          Cible SSH (user@host)
# =============================================================================
set -euo pipefail

ENV_ARG=""
DUMP_FILE="/tmp/lexicon.dump"
CREATE_DUMP=false
SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
DB_USER_OVERRIDE=""
DB_NAME_OVERRIDE=""
SKIP_RESTART=false
SKIP_TRANSFER=false
DRY_RUN=false

LOCAL_CONTAINER="postgres-lama-debug"
LOCAL_DB="lama_dev"
LOCAL_USER="lama_dev"

REMOTE_DUMP="/tmp/lexicon.dump"

log()  { printf '\033[0;36m[LEXICON-RESTORE]\033[0m %s\n' "$*"; }
err()  { printf '\033[0;31m[LEXICON-RESTORE][ERREUR]\033[0m %s\n' "$*" >&2; exit 1; }
ok()   { printf '\033[0;32m[LEXICON-RESTORE][OK]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[LEXICON-RESTORE][WARN]\033[0m %s\n' "$*"; }

usage() { grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \?//'; exit 0; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)             ENV_ARG="$2"; shift 2 ;;
    --dump)            DUMP_FILE="$2"; shift 2 ;;
    --create-dump)     CREATE_DUMP=true; shift ;;
    --ssh-key)         SSH_KEY_FILE="$2"; shift 2 ;;
    --target)          REMOTE_TARGET="$2"; shift 2 ;;
    --db-user)         DB_USER_OVERRIDE="$2"; shift 2 ;;
    --db-name)         DB_NAME_OVERRIDE="$2"; shift 2 ;;
    --skip-restart)    SKIP_RESTART=true; shift ;;
    --skip-transfer)   SKIP_TRANSFER=true; shift ;;
    --local-container) LOCAL_CONTAINER="$2"; shift 2 ;;
    --local-db)        LOCAL_DB="$2"; shift 2 ;;
    --local-user)      LOCAL_USER="$2"; shift 2 ;;
    --dry-run)         DRY_RUN=true; shift ;;
    -h|--help)         usage ;;
    *) err "Option inconnue : $1" ;;
  esac
done

[[ -z "$ENV_ARG" ]] && err "--env est requis (prod|staging|both)."
case "$ENV_ARG" in
  prod)    ENVS=(prod) ;;
  staging) ENVS=(staging) ;;
  both)    ENVS=(staging prod) ;;
  *) err "--env doit être : prod, staging ou both." ;;
esac

SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

command -v ssh  >/dev/null 2>&1 || err "ssh introuvable."
command -v scp  >/dev/null 2>&1 || err "scp introuvable."
command -v docker >/dev/null 2>&1 || err "docker introuvable (nécessaire pour --create-dump / checksum)."

run_remote() {
  local cmd="$1"
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "%s"\n' "$REMOTE_TARGET" "$cmd"
    return 0
  fi
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "$cmd"
}

# ── 1. Générer le dump depuis la base dev (optionnel) ───────────────────────
if [[ "$CREATE_DUMP" == "true" ]]; then
  log "Génération du dump lexicon depuis '$LOCAL_CONTAINER' ($LOCAL_DB)..."
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m docker exec %s pg_dump ... -Fc -Z6 -f /tmp/lexicon.dump\n' "$LOCAL_CONTAINER"
  else
    docker exec "$LOCAL_CONTAINER" pg_dump -U "$LOCAL_USER" -d "$LOCAL_DB" \
      --schema=lexicon --no-owner --no-privileges -Fc -Z6 -f /tmp/lexicon.dump
    docker cp "$LOCAL_CONTAINER:/tmp/lexicon.dump" "$DUMP_FILE"
    docker exec "$LOCAL_CONTAINER" rm -f /tmp/lexicon.dump
    ok "Dump créé : $DUMP_FILE ($(du -h "$DUMP_FILE" | cut -f1))"
  fi
fi

[[ "$DRY_RUN" == "false" && ! -f "$DUMP_FILE" ]] && \
  err "Dump introuvable : $DUMP_FILE (utilisez --create-dump ou --dump <file>)."

# ── 2. Checksum local ───────────────────────────────────────────────────────
LOCAL_SHA=""
if [[ "$DRY_RUN" == "false" ]]; then
  LOCAL_SHA="$(sha256sum "$DUMP_FILE" | cut -d' ' -f1)"
  log "SHA256 local : $LOCAL_SHA"
fi

# ── 3. Transfert vers le VPS (une seule fois) ───────────────────────────────
if [[ "$SKIP_TRANSFER" == "true" ]]; then
  warn "Transfert ignoré (--skip-transfer) : réutilisation de $REMOTE_DUMP sur le VPS."
else
  log "Transfert du dump vers $REMOTE_TARGET:$REMOTE_DUMP ..."
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m scp %s %s:%s\n' "$DUMP_FILE" "$REMOTE_TARGET" "$REMOTE_DUMP"
  else
    scp "${SSH_ARGS[@]}" "$DUMP_FILE" "$REMOTE_TARGET:$REMOTE_DUMP"
    REMOTE_SHA="$(run_remote "sha256sum '$REMOTE_DUMP' | cut -d' ' -f1")"
    [[ "$REMOTE_SHA" == "$LOCAL_SHA" ]] || err "Checksum distant ($REMOTE_SHA) ≠ local ($LOCAL_SHA)."
    ok "Transfert vérifié (checksum identique)."
  fi
fi

# ── 4. Restore par environnement ────────────────────────────────────────────
restore_env() {
  local env="$1"
  local pg_container="postgres-lama-$env"
  local server_container="lama-server-$env"
  local aiserver_container="lama-aiserver-fr-$env"
  local db_user="${DB_USER_OVERRIDE:-lama_$env}"
  local db_name="${DB_NAME_OVERRIDE:-lama_$env}"
  local do_restart="true"
  [[ "$SKIP_RESTART" == "true" ]] && do_restart="false"

  log "════════════════════════════════════════════════════"
  log " Restore lexicon → $(echo "$env" | tr '[:lower:]' '[:upper:]')"
  log " Conteneur PG : $pg_container  (db=$db_name user=$db_user)"
  log "════════════════════════════════════════════════════"

  local remote_script
  remote_script=$(cat <<EOR
set -e
PG="$pg_container"
DB="$db_name"
DBUSER="$db_user"
DUMP="$REMOTE_DUMP"

if ! docker ps --format '{{.Names}}' | grep -qx "\$PG"; then
  echo "[VPS][ERREUR] Conteneur \$PG introuvable ou non démarré." >&2
  exit 1
fi

echo "[VPS] Copie du dump dans le conteneur..."
docker cp "\$DUMP" "\$PG:/tmp/lexicon-restore.dump"

echo "[VPS] Compteurs AVANT :"
docker exec "\$PG" psql -U "\$DBUSER" -d "\$DB" -tAc \
  "SELECT COALESCE((SELECT string_agg(language_code||'='||c,' ') FROM (SELECT language_code, COUNT(*) c FROM lexicon.words GROUP BY 1) q),'(schéma absent)')" 2>/dev/null \
  || echo "  (schéma lexicon absent)"

echo "[VPS] DROP SCHEMA lexicon CASCADE (n'affecte pas sessions/games)..."
docker exec "\$PG" psql -U "\$DBUSER" -d "\$DB" -v ON_ERROR_STOP=1 -c "DROP SCHEMA IF EXISTS lexicon CASCADE;"

echo "[VPS] pg_restore (remap propriétaire → \$DBUSER)..."
docker exec "\$PG" pg_restore -U "\$DBUSER" -d "\$DB" --no-owner --no-privileges /tmp/lexicon-restore.dump

echo "[VPS] Compteurs APRÈS :"
docker exec "\$PG" psql -U "\$DBUSER" -d "\$DB" -c \
  "SELECT w.language_code, COUNT(DISTINCT w.word_id) mots, COUNT(DISTINCT d.definition_id) defs, COUNT(DISTINCT s.synonym_id) syns
   FROM lexicon.words w
   LEFT JOIN lexicon.definitions d ON d.word_id = w.word_id
   LEFT JOIN lexicon.synonyms s ON s.word_id = w.word_id
   GROUP BY 1 ORDER BY 1;"

docker exec "\$PG" rm -f /tmp/lexicon-restore.dump

if [ "$do_restart" = "true" ]; then
  echo "[VPS] Redémarrage server + aiserver (rechargement dictionnaire en mémoire)..."
  docker restart "$server_container" "$aiserver_container" >/dev/null && echo "  OK"
else
  echo "[VPS] Restart ignoré (--skip-restart)."
fi
echo "[VPS] Restore \$DB terminé."
EOR
)

  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "bash -s" <<SCRIPT\n%s\nSCRIPT\n' "$REMOTE_TARGET" "$remote_script"
  else
    ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$remote_script"
    ok "Restore $env terminé."
  fi
}

for env in "${ENVS[@]}"; do
  restore_env "$env"
done

# ── 5. Nettoyage dump distant ───────────────────────────────────────────────
if [[ "$SKIP_TRANSFER" == "false" ]]; then
  run_remote "rm -f '$REMOTE_DUMP'" || true
fi

ok "Terminé pour : ${ENVS[*]}"
