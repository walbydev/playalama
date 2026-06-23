#!/usr/bin/env bash
# =============================================================================
# setup-vps.sh — Initialisation du VPS (à exécuter une seule fois)
# =============================================================================
# Ce script prépare le VPS pour accueillir les environnements PROD et STAGING.
#
# Usage:
#   bash tools/scripts/deploy/setup-vps.sh --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/deploy/setup-vps.sh --ssh-key ~/.ssh/playalama.key --dry-run
#   bash tools/scripts/deploy/setup-vps.sh --ssh-key ~/.ssh/playalama.key --bundle /chemin/local/repo
#
# Options:
#   --ssh-key <file>        Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --target <user@host>    Cible SSH (défaut: debian@playalama.online)
#   --repo-url <url>        URL du dépôt git (défaut: URL Gitea LAN)
#   --bundle <dir>          Répertoire git local à bundler et pousser via SSH
#                           (utiliser quand le VPS ne peut pas atteindre Gitea)
#   --dry-run               Afficher les commandes sans les exécuter
#   -h, --help              Afficher cette aide
#
# IMPORTANT — Accès au dépôt git depuis le VPS :
#   Par défaut, le script clone depuis l'URL Gitea (192.168.30.20:3000).
#   Cette adresse est une IP LAN PRIVÉE : le VPS sur internet ne peut PAS
#   l'atteindre directement. Dans ce cas, utiliser --bundle <dir> pour
#   packager le repo local et l'envoyer sur le VPS via SSH.
#   Le bare repo est créé dans /opt/playalama/git/playalama.git sur le VPS.
#   Pour les déploiements futurs : make push-bundle SSH_KEY=...
#
# Ce script :
#   1. Installe Docker + Docker Compose (si absents)
#   2. Crée la structure de répertoires /opt/playalama/traefik/ et /srv/playalama/{prod,staging}/
#   3. Clone le dépôt git dans prod (branche master) et staging (branche master)
#      → via git clone (si --repo-url accessible) ou via --bundle (git bundle SSH)
#   4. Crée le réseau Docker "traefik-net"
#   5. Déploie Traefik depuis /opt/playalama/traefik/
#   6. Affiche les instructions pour créer les fichiers .env
# =============================================================================
set -euo pipefail

SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
REPO_URL=""
BUNDLE_SOURCE_DIR=""   # chemin local du dépôt à bundler
DRY_RUN=false

# URL Gitea LAN (accessible uniquement depuis le réseau local — PAS depuis le VPS internet)
GITEA_DEFAULT_URL="http://192.168.30.20:3000/WalbyGaming/playalama.git"

usage() {
  grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \?//'
  exit 0
}

log()  { printf '\033[0;36m[SETUP]\033[0m %s\n' "$*"; }
err()  { printf '\033[0;31m[SETUP][ERREUR]\033[0m %s\n' "$*" >&2; exit 1; }
ok()   { printf '\033[0;32m[SETUP][OK]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[SETUP][WARN]\033[0m %s\n' "$*"; }

run_remote() {
  local cmd="$1"
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "%s"\n' "$REMOTE_TARGET" "$cmd"
    return 0
  fi
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "$cmd"
}

copy_file() {
  local local_path="$1"
  local remote_path="$2"
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m scp %s → %s:%s\n' "$local_path" "$REMOTE_TARGET" "$remote_path"
    return 0
  fi
  scp "${SSH_ARGS[@]}" "$local_path" "$REMOTE_TARGET:$remote_path"
}

# Parsing des arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --ssh-key)  SSH_KEY_FILE="$2"; shift 2 ;;
    --target)   REMOTE_TARGET="$2"; shift 2 ;;
    --repo-url) REPO_URL="$2"; shift 2 ;;
    --bundle)   BUNDLE_SOURCE_DIR="$2"; shift 2 ;;
    --dry-run)  DRY_RUN=true; shift ;;
    -h|--help)  usage ;;
    *) err "Option inconnue: $1" ;;
  esac
done

# Détection de l'URL du dépôt git si non fournie
# Priorité : --repo-url > LAMA_DEPLOY_REPO_URL > URL Gitea LAN par défaut
if [[ -z "$REPO_URL" ]]; then
  REPO_URL="${LAMA_DEPLOY_REPO_URL:-$GITEA_DEFAULT_URL}"
fi

# Arguments SSH
SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

log "════════════════════════════════════════════════════"
log " Setup VPS playlama — $(date '+%Y-%m-%d %H:%M')"
log " Cible SSH  : $REMOTE_TARGET"
if [[ -n "$BUNDLE_SOURCE_DIR" ]]; then
  log " Mode git   : bundle SSH depuis $BUNDLE_SOURCE_DIR"
else
  log " Dépôt git  : $REPO_URL"
fi
[[ "$DRY_RUN" == "true" ]] && warn " MODE DRY-RUN activé — aucune modification réelle"
log "════════════════════════════════════════════════════"

# ─────────────────────────────────────────────────────────
# 1. Installation Docker
# ─────────────────────────────────────────────────────────
log "[1/6] Vérification/installation Docker..."
run_remote "
  if ! command -v docker >/dev/null 2>&1; then
    echo '→ Installation Docker...'
    curl -fsSL https://get.docker.com | sh
    sudo usermod -aG docker \$USER
    echo '→ Docker installé. Reconnexion SSH nécessaire si c''est la première fois.'
  else
    echo '→ Docker déjà présent: '$(docker --version)
  fi
"

# ─────────────────────────────────────────────────────────
# 2. Structure de répertoires
# ─────────────────────────────────────────────────────────
log "[2/6] Création de la structure de répertoires..."
run_remote "
  set -e
  sudo mkdir -p /opt/playalama/traefik/logs
  sudo mkdir -p /srv/playalama/prod
  sudo mkdir -p /srv/playalama/staging
  sudo chown -R \$(whoami):\$(whoami) /opt/playalama /srv/playalama
  echo '→ Répertoires créés :'
  echo '   /opt/playalama/traefik/'
  echo '   /srv/playalama/prod/'
  echo '   /srv/playalama/staging/'
"

# ─────────────────────────────────────────────────────────
# 3. Clone du dépôt git
# ─────────────────────────────────────────────────────────
if [[ -n "$BUNDLE_SOURCE_DIR" ]]; then
  # ── Mode bundle : le VPS ne peut pas atteindre Gitea (IP LAN) ──────────────
  log "[3/6] Clonage via bundle SSH depuis $BUNDLE_SOURCE_DIR..."

  BUNDLE_TMP="/tmp/playalama-setup-$$.bundle"

  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m git bundle create %s --branches --tags (dans %s)\n' "$BUNDLE_TMP" "$BUNDLE_SOURCE_DIR"
    printf '\033[0;33m[DRY-RUN]\033[0m scp %s → %s:/tmp/playalama.bundle\n' "$BUNDLE_TMP" "$REMOTE_TARGET"
  else
    log "  → Création du bundle git..."
    git -C "$BUNDLE_SOURCE_DIR" bundle create "$BUNDLE_TMP" --branches --tags
    log "  → Copie vers le VPS..."
    scp "${SSH_ARGS[@]}" "$BUNDLE_TMP" "$REMOTE_TARGET:/tmp/playalama.bundle"
    rm -f "$BUNDLE_TMP"
  fi

  run_remote "
    set -e
    BARE=/opt/playalama/git/playalama.git
    BUNDLE=/tmp/playalama.bundle

    if [ -d \"\$BARE\" ]; then
      echo '→ bare repo déjà présent, mise à jour...'
      git --git-dir=\"\$BARE\" fetch \$BUNDLE 'refs/heads/*:refs/heads/*'
    else
      echo '→ Création du bare repo depuis le bundle...'
      git clone --bare \$BUNDLE \$BARE
    fi

    # PROD
    if [ -d '/srv/playalama/prod/.git' ]; then
      echo '→ prod: dépôt déjà présent, git pull...'
      cd /srv/playalama/prod && git pull origin master
    else
      echo '→ prod: clonage depuis bare repo local...'
      git clone \$BARE /srv/playalama/prod
      cd /srv/playalama/prod && git checkout master
    fi

    # STAGING
    if [ -d '/srv/playalama/staging/.git' ]; then
      echo '→ staging: dépôt déjà présent, git pull...'
      cd /srv/playalama/staging && git pull origin master
    else
      echo '→ staging: clonage depuis bare repo local...'
      git clone \$BARE /srv/playalama/staging
      cd /srv/playalama/staging && git checkout master
    fi

    rm -f \$BUNDLE
    echo '→ Dépôts prod et staging prêts (origin = bare repo local VPS)'
  "

else
  # ── Mode URL : clone direct depuis Gitea (accès réseau requis) ─────────────
  log "[3/6] Clonage du dépôt git depuis $REPO_URL..."
  run_remote "
    set -e
    # Désactiver la vérification SSL pour le cert auto-signé de Gitea (une seule fois)
    git config --global http.sslVerify false

    # PROD (branche master)
    if [ -d '/srv/playalama/prod/.git' ]; then
      echo '→ prod: dépôt déjà présent, git pull...'
      cd /srv/playalama/prod && git fetch origin && git pull origin master
    else
      echo '→ prod: clonage branche master...'
      git clone --branch master '$REPO_URL' /srv/playalama/prod
    fi

    # STAGING (branche master)
    if [ -d '/srv/playalama/staging/.git' ]; then
      echo '→ staging: dépôt déjà présent, git pull...'
      cd /srv/playalama/staging && git fetch origin && git pull origin master
    else
      echo '→ staging: clonage branche master...'
      git clone --branch master '$REPO_URL' /srv/playalama/staging
    fi
  "
fi

# ─────────────────────────────────────────────────────────
# 4. Déploiement Traefik
# ─────────────────────────────────────────────────────────
log "[4/6] Déploiement de Traefik..."

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Copier la config Traefik
copy_file "$ROOT_DIR/tools/docker/traefik.yml"              "/opt/playalama/traefik/traefik.yml"
copy_file "$ROOT_DIR/tools/docker/docker-compose.traefik.yml" "/opt/playalama/traefik/docker-compose.yml"

# Créer acme.json avec les bons droits
run_remote "
  set -e
  if [ ! -f '/opt/playalama/traefik/acme.json' ]; then
    touch /opt/playalama/traefik/acme.json
    chmod 600 /opt/playalama/traefik/acme.json
    echo '→ acme.json créé (chmod 600)'
  else
    echo '→ acme.json déjà présent'
  fi
"

# ─────────────────────────────────────────────────────────
# 5. Démarrage de Traefik (le réseau traefik-net est créé par Compose)
# ─────────────────────────────────────────────────────────
log "[5/6] Démarrage de Traefik..."

# Si le réseau existe sans labels Compose (créé manuellement par erreur), le supprimer
run_remote "
  if docker network inspect traefik-net >/dev/null 2>&1; then
    LABELS=\$(docker network inspect traefik-net --format '{{index .Labels \"com.docker.compose.network\"}}')
    if [ -z \"\$LABELS\" ]; then
      echo '→ Réseau traefik-net sans labels Compose — suppression avant redéploiement...'
      docker network rm traefik-net
    else
      echo '→ Réseau traefik-net déjà géré par Compose'
    fi
  fi
"

# Démarrer Traefik (docker compose crée le réseau traefik-net avec les bons labels)
run_remote "
  set -e
  cd /opt/playalama/traefik
  docker compose pull
  docker compose up -d
  echo '→ Traefik démarré'
"

# ─────────────────────────────────────────────────────────
# 6. Instructions finales
# ─────────────────────────────────────────────────────────
log "[6/6] Setup terminé !"
echo ""
ok "════════════════════════════════════════════════════"
ok " VPS configuré avec succès 🦙"
ok "════════════════════════════════════════════════════"
echo ""
warn "ÉTAPES MANUELLES REQUISES :"
echo ""
echo "  1. Créer le fichier .env PROD sur le VPS :"
echo "     scp tools/docker/.env.prod.example ${REMOTE_TARGET}:/srv/playalama/prod/.env"
echo "     # Puis éditer : ssh ${REMOTE_TARGET} 'nano /srv/playalama/prod/.env'"
echo ""
echo "  2. Créer le fichier .env STAGING sur le VPS :"
echo "     scp tools/docker/.env.staging.example ${REMOTE_TARGET}:/srv/playalama/staging/.env"
echo "     # Puis éditer : ssh ${REMOTE_TARGET} 'nano /srv/playalama/staging/.env'"
echo ""
echo "  3. Premier déploiement :"
echo "     make deploy-prod SSH_KEY=${SSH_KEY_FILE:-~/.ssh/playalama.key}"
echo "     make deploy-staging SSH_KEY=${SSH_KEY_FILE:-~/.ssh/playalama.key}"
echo ""
echo "  4. Dashboard Traefik (depuis le VPS) :"
echo "     ssh ${REMOTE_TARGET} -L 8080:localhost:8080"
echo "     → http://localhost:8080"
echo ""
