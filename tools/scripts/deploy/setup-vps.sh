#!/usr/bin/env bash
# =============================================================================
# setup-vps.sh — Initialisation du VPS (idempotent, sans code source)
# =============================================================================
# Prépare le VPS pour accueillir les environnements PROD et STAGING.
# Stratégie : image-only — le VPS ne compile RIEN, pas de git clone.
#
# Usage:
#   bash tools/scripts/deploy/setup-vps.sh --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/deploy/setup-vps.sh --ssh-key ~/.ssh/playalama.key --dry-run
#
# Options:
#   --ssh-key <file>        Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --target <user@host>    Cible SSH (défaut: debian@playalama.online)
#   --dry-run               Afficher les commandes sans les exécuter
#   -h, --help              Afficher cette aide
#
# Ce script :
#   1. Installe Docker + outils (si absents)
#   2. Crée la structure de répertoires :
#        /opt/playalama/traefik/          ← Traefik
#        /opt/playalama/prod/             ← PROD (compose + assets + artifacts)
#        /opt/playalama/staging/          ← STAGING (compose + assets + artifacts)
#   3. Déploie Traefik (crée le réseau traefik-net)
#   4. Affiche les instructions pour copier les fichiers .env
#
# Après setup-vps, lancer dans l'ordre :
#   1. Copier les .env sur le VPS (voir instructions à la fin)
#   2. make deploy ENV=prod SSH_KEY=...
#   3. make deploy ENV=staging SSH_KEY=...
# =============================================================================
set -euo pipefail

SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
DRY_RUN=false

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

usage() {
  grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \?//'
  exit 0
}

log()  { printf '\033[0;36m[SETUP]\033[0m %s\n' "$*"; }
err()  { printf '\033[0;31m[SETUP][ERREUR]\033[0m %s\n' "$*" >&2; exit 1; }
ok()   { printf '\033[0;32m[SETUP][OK]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[SETUP][WARN]\033[0m %s\n' "$*"; }

run_remote() {
  if [[ "$DRY_RUN" == "true" ]]; then
    printf '\033[0;33m[DRY-RUN]\033[0m ssh %s "..."\n' "$REMOTE_TARGET"
    printf '%s\n' "$1" | sed 's/^/  /'
    return 0
  fi
  ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$1"
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

while [[ $# -gt 0 ]]; do
  case "$1" in
    --ssh-key) SSH_KEY_FILE="$2"; shift 2 ;;
    --target)  REMOTE_TARGET="$2"; shift 2 ;;
    --dry-run) DRY_RUN=true; shift ;;
    -h|--help) usage ;;
    *) err "Option inconnue: $1" ;;
  esac
done

SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

log "════════════════════════════════════════════════════"
log " Setup VPS Playalama — $(date '+%Y-%m-%d %H:%M')"
log " Cible SSH  : $REMOTE_TARGET"
log " Mode       : image-only (pas de code source sur le VPS)"
[[ "$DRY_RUN" == "true" ]] && warn " MODE DRY-RUN activé — aucune modification réelle"
log "════════════════════════════════════════════════════"

# ─────────────────────────────────────────────────────────
# 1. Installation Docker + outils
# ─────────────────────────────────────────────────────────
log "[1/4] Vérification/installation Docker..."
run_remote '
set -e
if ! command -v docker >/dev/null 2>&1; then
  echo "→ Installation Docker..."
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker $(whoami)
  echo "→ Docker installé."
  echo "  ⚠  Si première installation, déconnecter/reconnecter la session SSH."
else
  echo "→ Docker déjà présent: $(docker --version)"
fi

# curl pour les healthchecks (souvent absent sur Debian minimal)
if ! command -v curl >/dev/null 2>&1; then
  echo "→ Installation curl..."
  sudo apt-get update -qq && sudo apt-get install -y -qq curl
fi
'

# ─────────────────────────────────────────────────────────
# 2. Structure de répertoires (image-only, pas de git clone)
# ─────────────────────────────────────────────────────────
log "[2/4] Création de la structure de répertoires..."
run_remote '
set -e
# Traefik
sudo mkdir -p /opt/playalama/traefik/logs

# PROD
sudo mkdir -p /opt/playalama/prod/assets/languages
sudo mkdir -p /opt/playalama/prod/artifacts/zip
sudo mkdir -p /opt/playalama/prod/tools/docker

# STAGING
sudo mkdir -p /opt/playalama/staging/assets/languages
sudo mkdir -p /opt/playalama/staging/artifacts/zip
sudo mkdir -p /opt/playalama/staging/tools/docker

sudo chown -R $(whoami):$(whoami) /opt/playalama

echo "→ Structure créée :"
echo "   /opt/playalama/traefik/"
echo "   /opt/playalama/prod/    (assets/, artifacts/, tools/docker/)"
echo "   /opt/playalama/staging/ (assets/, artifacts/, tools/docker/)"
'

# ─────────────────────────────────────────────────────────
# 3. Déploiement Traefik
# ─────────────────────────────────────────────────────────
log "[3/4] Déploiement de Traefik..."

for f in traefik.yml docker-compose.traefik.yml nginx-docker-api-proxy.conf; do
  local_f="$ROOT_DIR/tools/docker/$f"
  [[ ! -f "$local_f" ]] && { warn "Fichier manquant: $local_f — ignoré"; continue; }
  remote_f="/opt/playalama/traefik/$f"
  [[ "$f" == "docker-compose.traefik.yml" ]] && remote_f="/opt/playalama/traefik/docker-compose.yml"
  copy_file "$local_f" "$remote_f"
done

run_remote '
set -e
# Créer acme.json avec les bons droits (idempotent)
if [ ! -f /opt/playalama/traefik/acme.json ]; then
  touch /opt/playalama/traefik/acme.json
  chmod 600 /opt/playalama/traefik/acme.json
  echo "→ acme.json créé (chmod 600)"
else
  echo "→ acme.json déjà présent"
fi

# Nettoyer réseau traefik-net sans labels Compose (créé manuellement par erreur)
if docker network inspect traefik-net >/dev/null 2>&1; then
  LABELS=$(docker network inspect traefik-net --format "{{index .Labels \"com.docker.compose.network\"}}")
  if [ -z "$LABELS" ]; then
    echo "→ Réseau traefik-net sans labels Compose — suppression avant redéploiement..."
    docker network rm traefik-net
  else
    echo "→ Réseau traefik-net déjà géré par Compose"
  fi
fi

# Démarrer Traefik
cd /opt/playalama/traefik
docker compose pull
docker compose up -d
echo "→ Traefik démarré"
'

# ─────────────────────────────────────────────────────────
# 4. Instructions finales
# ─────────────────────────────────────────────────────────
log "[4/4] Setup terminé !"
echo ""
ok "════════════════════════════════════════════════════"
ok " VPS configuré avec succès 🦙"
ok "════════════════════════════════════════════════════"
echo ""
warn "ÉTAPES SUIVANTES :"
echo ""
echo "  1. Copier le fichier .env PROD sur le VPS :"
printf '     scp tools/docker/.env.prod.example %s:/opt/playalama/prod/.env\n' "$REMOTE_TARGET"
printf '     ssh %s "nano /opt/playalama/prod/.env"\n' "$REMOTE_TARGET"
echo ""
echo "  2. Copier le fichier .env STAGING sur le VPS :"
printf '     scp tools/docker/.env.staging.example %s:/opt/playalama/staging/.env\n' "$REMOTE_TARGET"
printf '     ssh %s "nano /opt/playalama/staging/.env"\n' "$REMOTE_TARGET"
echo ""
echo "  3. Premier déploiement :"
printf '     make deploy ENV=prod    SSH_KEY=%s\n' "${SSH_KEY_FILE:-~/.ssh/playalama.key}"
printf '     make deploy ENV=staging SSH_KEY=%s\n' "${SSH_KEY_FILE:-~/.ssh/playalama.key}"
echo ""
echo "  4. Dashboard Traefik (tunnel SSH local) :"
printf '     ssh %s -L 8080:localhost:8080\n' "$REMOTE_TARGET"
echo "     → http://localhost:8080"
echo ""
