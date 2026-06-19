#!/usr/bin/env bash
set -euo pipefail

BASE_DIR="/opt/playalama"
APPLY="false"
BACKUP_ENABLED="true"
VERBOSE="false"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_ROOT=""

log() {
  printf '[ORDER] %s\n' "$*"
}

log_info() {
  printf '[INFO] %s\n' "$*"
}

log_warn() {
  printf '[WARN] %s\n' "$*" >&2
}

log_error() {
  printf '[ERROR] %s\n' "$*" >&2
}

run_cmd() {
  if [[ "$APPLY" == "true" ]]; then
    "$@"
  else
    printf '[DRY-RUN]'
    printf ' %q' "$@"
    printf '\n'
  fi
}

ensure_dir() {
  local dir="$1"
  if [[ ! -d "$dir" ]]; then
    log_info "Création du dossier: $dir"
    run_cmd mkdir -p "$dir"
  fi
}

backup_path() {
  local src="$1"
  [[ "$BACKUP_ENABLED" == "true" ]] || return 0
  [[ -e "$src" ]] || return 0

  local rel="$src"
  if [[ "$src" == "$BASE_DIR"/* ]]; then
    rel="${src#${BASE_DIR}/}"
  else
    rel="$(basename "$src")"
  fi

  local dest="$BACKUP_ROOT/$rel"
  if [[ "$APPLY" == "true" ]]; then
    mkdir -p "$(dirname "$dest")"
    cp -a "$src" "$dest"
  else
    printf '[DRY-RUN] backup %q -> %q\n' "$src" "$dest"
  fi
}

same_file() {
  local left="$1"
  local right="$2"
  [[ -f "$left" && -f "$right" ]] || return 1
  cmp -s "$left" "$right"
}

normalize_site_layout() {
  local site_root="$BASE_DIR/site"
  local static_root="$site_root/static"

  if [[ -f "$site_root/index.html" && ! -f "$static_root/index.html" ]]; then
    log_info "Migration accueil: $site_root/index.html -> $static_root/index.html"
    backup_path "$site_root/index.html"
    run_cmd mkdir -p "$static_root"
    run_cmd mv "$site_root/index.html" "$static_root/index.html"
  fi

  if [[ -d "$site_root/download" && ! -d "$static_root/download" ]]; then
    log_info "Migration download: $site_root/download -> $static_root/download"
    backup_path "$site_root/download"
    run_cmd mkdir -p "$static_root"
    run_cmd mv "$site_root/download" "$static_root/download"
  fi

  if [[ ! -d "$static_root/download" ]]; then
    run_cmd mkdir -p "$static_root/download"
  fi
}

flatten_assets_dir() {
  local nested_dir="$1"
  [[ -d "$nested_dir" ]] || return 0

  local parent_dir
  parent_dir="$(dirname "$nested_dir")"

  log_info "Normalisation: $nested_dir"
  backup_path "$nested_dir"

  shopt -s nullglob dotglob
  local entries=("$nested_dir"/*)
  shopt -u nullglob dotglob

  if [[ ${#entries[@]} -eq 0 ]]; then
    log_warn "Répertoire vide: $nested_dir"
    if [[ "$APPLY" == "true" ]]; then
      rmdir "$nested_dir" 2>/dev/null || true
    fi
    return 0
  fi

  local entry dest
  for entry in "${entries[@]}"; do
    dest="$parent_dir/$(basename "$entry")"
    if [[ -e "$dest" ]]; then
      if same_file "$entry" "$dest"; then
        log_info "Déjà en place: $(basename "$entry")"
        if [[ "$APPLY" == "true" ]]; then
          rm -f "$entry"
        fi
      else
        log_warn "Conflit conservé (manuel): $entry existe aussi à $dest"
      fi
      continue
    fi

    log_info "Déplacement: $entry -> $dest"
    run_cmd mv "$entry" "$dest"
  done

  if [[ "$APPLY" == "true" ]]; then
    rmdir "$nested_dir" 2>/dev/null || true
  fi
}

usage() {
  cat <<'EOF'
Usage: organize-vps-playalama.sh [options]

Remet en ordre une installation VPS Playalama déjà existante.
Par défaut, le script est en mode simulation (dry-run).

Options:
  --base-dir <path>    Racine de l'installation VPS (défaut: /opt/playalama)
  --apply              Appliquer réellement les modifications
  --dry-run            Forcer le mode simulation
  --no-backup          Ne pas sauvegarder les chemins modifiés
  --verbose            Sortie détaillée
  -h, --help           Afficher ce message

Ce que le script normalise:
  - `site/index.html` -> `site/static/index.html` si le site a une ancienne structure
  - `site/download/` -> `site/static/download/` si nécessaire
  - `*/assets/languages/fr/assets/*` -> `*/assets/languages/fr/*` (flatten)

Exemples:
  ./organize-vps-playalama.sh --base-dir /opt/playalama --apply
  ./organize-vps-playalama.sh --base-dir /opt/playalama --dry-run
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-dir)
      BASE_DIR="$2"
      shift 2
      ;;
    --apply)
      APPLY="true"
      shift
      ;;
    --dry-run)
      APPLY="false"
      shift
      ;;
    --no-backup)
      BACKUP_ENABLED="false"
      shift
      ;;
    --verbose)
      VERBOSE="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      log_error "Option inconnue: $1"
      usage
      exit 1
      ;;
  esac
done

if [[ "$APPLY" == "true" ]]; then
  BACKUP_ROOT="$BASE_DIR/.maintenance/backups/$TIMESTAMP"
  log_info "Sauvegarde de sécurité: $BACKUP_ROOT"
  run_cmd mkdir -p "$BACKUP_ROOT"
fi

if [[ ! -d "$BASE_DIR" ]]; then
  log_error "Base introuvable: $BASE_DIR"
  exit 1
fi

log "Base de travail: $BASE_DIR"
log "Mode: $([[ "$APPLY" == "true" ]] && printf 'apply' || printf 'dry-run')"

ensure_dir "$BASE_DIR/site"
ensure_dir "$BASE_DIR/site/static"
ensure_dir "$BASE_DIR/site/static/assets"

normalize_site_layout

while IFS= read -r -d '' nested_dir; do
  flatten_assets_dir "$nested_dir"
done < <(find "$BASE_DIR" -type d -path '*/assets/languages/fr/assets' -print0)

if [[ "$VERBOSE" == "true" ]]; then
  log_info "Aperçu final (depth 4):"
  find "$BASE_DIR" -maxdepth 4 \( -path '*/.maintenance/*' -o -path '*/.git/*' \) -prune -o -print | sort | sed -n '1,200p'
fi

log "Terminé"

