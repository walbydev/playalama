#!/usr/bin/env bash
# =============================================================================
# setup-vps.sh — Initialisation du VPS (à exécuter une seule fois)
# =============================================================================
# Ce script prépare le VPS pour accueillir les environnements PROD et STAGING.
#
# Usage:
#   bash tools/deployments/setup-vps.sh --ssh-key ~/.ssh/playalama.key
#   bash tools/deployments/setup-vps.sh --ssh-key ~/.ssh/playalama.key --dry-run
#
# Options:
#   --ssh-key <file>        Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --target <user@host>    Cible SSH (défaut: debian@playalama.online)
#   --repo-url <url>        URL du dépôt git (défaut: depuis git remote origin)
#   --dry-run               Afficher les commandes sans les exécuter
#   -h, --help              Afficher cette aide
#
# Ce script :
#   1. Installe Docker + Docker Compose (si absents)
#   2. Crée la structure de répertoires /opt/playalama/traefik/ et /srv/playalama/{prod,staging}/
#   3. Clone le dépôt git dans prod (branche main) et staging (branche staging)
#   4. Crée le réseau Docker "traefik-net"
#   5. Déploie Traefik depuis /opt/playalama/traefik/
#   6. Affiche les instructions pour créer les fichiers .env
# =============================================================================
set -euo pipefail

SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
REPO_URL=""
DRY_RUN=false

# URL Gitea par défaut (cert auto-signé : git http.sslVerify désactivé sur le VPS)
GITEA_DEFAULT_URL="https://gitea.home.lan/WalbyGaming/playalama.git"

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
    --dry-run)  DRY_RUN=true; shift ;;
    -h|--help)  usage ;;
    *) err "Option inconnue: $1" ;;
  esac
done

# Détection de l'URL du dépôt git si non fournie
# Priorité : --repo-url > LAMA_DEPLOY_REPO_URL > URL Gitea par défaut
if [[ -z "$REPO_URL" ]]; then
  REPO_URL="${LAMA_DEPLOY_REPO_URL:-$GITEA_DEFAULT_URL}"
fi

# Arguments SSH
SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

log "════════════════════════════════════════════════════"
log " Setup VPS playlama — $(date '+%Y-%m-%d %H:%M')"
log " Cible SSH  : $REMOTE_TARGET"
log " Dépôt git  : $REPO_URL"
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
# 3. Clone du dépôt git (Gitea — cert auto-signé)
# ─────────────────────────────────────────────────────────
log "[3/6] Clonage du dépôt git depuis Gitea ($REPO_URL)..."
run_remote "
  set -e
  # Désactiver la vérification SSL pour le cert auto-signé de Gitea (une seule fois)
  git config --global http.sslVerify false

  # PROD (branche main)
  if [ -d '/srv/playalama/prod/.git' ]; then
    echo '→ prod: dépôt déjà présent, git pull...'
    cd /srv/playalama/prod && git fetch origin && git pull origin main
  else
    echo '→ prod: clonage branche main...'
    git clone --branch main '$REPO_URL' /srv/playalama/prod
  fi

  # STAGING (branche staging, fallback sur main si inexistante)
  if [ -d '/srv/playalama/staging/.git' ]; then
    echo '→ staging: dépôt déjà présent, git pull...'
    cd /srv/playalama/staging && git fetch origin && \
      (git pull origin staging 2>/dev/null || git pull origin main)
  else
    echo '→ staging: clonage (branche staging ou main)...'
    git clone --branch staging '$REPO_URL' /srv/playalama/staging 2>/dev/null || \
    git clone --branch main '$REPO_URL' /srv/playalama/staging
  fi
"

# ─────────────────────────────────────────────────────────
# 4. Déploiement Traefik
# ─────────────────────────────────────────────────────────
log "[4/6] Déploiement de Traefik..."

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

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
# 5. Réseau Docker traefik-net
# ─────────────────────────────────────────────────────────
log "[5/6] Création du réseau Docker traefik-net..."
run_remote "
  if docker network inspect traefik-net >/dev/null 2>&1; then
    echo '→ Réseau traefik-net déjà existant'
  else
    docker network create traefik-net
    echo '→ Réseau traefik-net créé'
  fi
"

# Démarrer Traefik
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
echo "     scp tools/environments/.env.prod.example ${REMOTE_TARGET}:/srv/playalama/prod/.env"
echo "     # Puis éditer : ssh ${REMOTE_TARGET} 'nano /srv/playalama/prod/.env'"
echo ""
echo "  2. Créer le fichier .env STAGING sur le VPS :"
echo "     scp tools/environments/.env.staging.example ${REMOTE_TARGET}:/srv/playalama/staging/.env"
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
