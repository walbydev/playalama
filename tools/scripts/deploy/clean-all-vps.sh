#!/usr/bin/env bash
# =============================================================================
# clean-all-vps.sh — Remise à zéro COMPLÈTE du VPS
# =============================================================================
# ⚠  DESTRUCTIF — supprime TOUT : containers, images, volumes, répertoires.
#    Seuls Docker lui-même et Traefik sont conservés par défaut.
#
# Usage:
#   bash tools/scripts/deploy/clean-all-vps.sh --ssh-key ~/.ssh/playalama.key
#   bash tools/scripts/deploy/clean-all-vps.sh --ssh-key ~/.ssh/playalama.key --dry-run
#   bash tools/scripts/deploy/clean-all-vps.sh --ssh-key ~/.ssh/playalama.key --include-traefik
#   bash tools/scripts/deploy/clean-all-vps.sh --ssh-key ~/.ssh/playalama.key --yes
#
# Options:
#   --ssh-key <file>       Clé SSH (ou via LAMA_DEPLOY_SSH_KEY)
#   --target <user@host>   Cible SSH (défaut: debian@playalama.online)
#   --include-traefik      Supprimer aussi Traefik et acme.json (certs TLS perdus !)
#   --yes                  Pas de confirmation interactive
#   --dry-run              Afficher les commandes sans les exécuter
#   -h, --help             Afficher cette aide
#
# Ce qui est supprimé :
#   - Tous les containers lama-* (running ou stopped)
#   - Toutes les images Docker lama-*
#   - Tous les volumes Docker lama-*
#   - Répertoires /opt/playalama/prod/ et /opt/playalama/staging/
#   - Réseau Docker lama-* (internes aux stacks)
#   - Images Docker dangling (non taguées)
#
# Ce qui est conservé (sauf --include-traefik) :
#   - Traefik (container + image + config + acme.json)
#   - Réseau traefik-net
#   - Docker lui-même
#   - /opt/playalama/traefik/
# =============================================================================
set -euo pipefail

SSH_KEY_FILE="${LAMA_DEPLOY_SSH_KEY:-}"
REMOTE_TARGET="${LAMA_DEPLOY_TARGET:-debian@playalama.online}"
INCLUDE_TRAEFIK=false
AUTO_YES=false
DRY_RUN=false

usage() {
  grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \?//'
  exit 0
}

log()  { printf '\033[0;36m[CLEAN]\033[0m %s\n' "$*"; }
err()  { printf '\033[0;31m[CLEAN][ERR]\033[0m %s\n' "$*" >&2; exit 1; }
ok()   { printf '\033[0;32m[CLEAN][OK]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[CLEAN][WARN]\033[0m %s\n' "$*"; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --ssh-key)          SSH_KEY_FILE="$2"; shift 2 ;;
    --target)           REMOTE_TARGET="$2"; shift 2 ;;
    --include-traefik)  INCLUDE_TRAEFIK=true; shift ;;
    --yes|-y)           AUTO_YES=true; shift ;;
    --dry-run)          DRY_RUN=true; shift ;;
    -h|--help)          usage ;;
    *) err "Option inconnue: $1" ;;
  esac
done

SSH_ARGS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)
[[ -n "$SSH_KEY_FILE" ]] && SSH_ARGS+=(-i "$SSH_KEY_FILE")

# ── Confirmation ──────────────────────────────────────────────────────────────
echo ""
warn "════════════════════════════════════════════════════════════"
warn " ⚠  REMISE À ZÉRO COMPLÈTE DU VPS : $REMOTE_TARGET"
warn "════════════════════════════════════════════════════════════"
echo ""
echo "  Sera supprimé :"
echo "    • Tous les containers lama-*"
echo "    • Toutes les images Docker lama-*"
echo "    • Tous les volumes Docker lama-*"
echo "    • /opt/playalama/prod/"
echo "    • /opt/playalama/staging/"
echo "    • Images Docker dangling"
if [[ "$INCLUDE_TRAEFIK" == "true" ]]; then
  echo ""
  warn "  + --include-traefik activé :"
  warn "    • Container + image Traefik"
  warn "    • /opt/playalama/traefik/ (acme.json inclus — CERTS TLS PERDUS !)"
  warn "    • Réseau traefik-net"
fi
echo ""

if [[ "$DRY_RUN" == "true" ]]; then
  warn " MODE DRY-RUN — aucune modification réelle"
  echo ""
fi

if [[ "$AUTO_YES" == "false" && "$DRY_RUN" == "false" ]]; then
  printf '\033[0;31mTaper "clean" pour confirmer (tout autre chose annule) : \033[0m'
  read -r CONFIRM
  if [[ "$CONFIRM" != "clean" ]]; then
    echo "Annulé."
    exit 0
  fi
fi

# ── Script distant ────────────────────────────────────────────────────────────
CLEAN_SCRIPT=$(cat <<ENDSCRIPT
set -euo pipefail
INCLUDE_TRAEFIK="${INCLUDE_TRAEFIK}"

echo ""
echo "=== [1/6] Containers actifs avant nettoyage ==="
docker ps --format "  {{.Names}} ({{.Status}})"

echo ""
echo "=== [2/6] Arrêt et suppression des stacks depuis /opt/playalama ==="
for ENV in prod staging; do
  DIR="/opt/playalama/\$ENV"
  if [ -d "\$DIR" ] && [ -f "\$DIR/docker-compose.yml" ]; then
    echo "  Arrêt stack \$ENV depuis \$DIR..."
    cd "\$DIR" && docker compose -p "\$ENV" down --remove-orphans --volumes 2>/dev/null || true
  fi
done

echo ""
echo "=== Arrêt stacks depuis /srv/playalama (ancienne structure) ==="
for ENV in prod staging; do
  for SUBPATH in "\$ENV" ""; do
    OLD_DIR="/srv/playalama/\$SUBPATH"
    if [ -d "\$OLD_DIR" ] && [ -f "\$OLD_DIR/docker-compose.yml" ]; then
      echo "  Arrêt stack depuis \$OLD_DIR..."
      cd "\$OLD_DIR" && docker compose -p "\$ENV" down --remove-orphans 2>/dev/null || true
    fi
  done
done

echo ""
echo "=== [3/6] Suppression de tous les containers lama-* ==="
CONTAINERS=\$(docker ps -a --filter "name=lama-" --format "{{.Names}}" 2>/dev/null || true)
if [ -n "\$CONTAINERS" ]; then
  echo "\$CONTAINERS" | xargs docker rm -f
  echo "  Supprimés: \$(echo \$CONTAINERS | tr '\n' ' ')"
else
  echo "  Aucun container lama-* trouvé"
fi

echo ""
echo "=== [4/6] Suppression de toutes les images Docker lama-* ==="
IMAGES=\$(docker images --filter "reference=lama-*" -q 2>/dev/null || true)
if [ -n "\$IMAGES" ]; then
  echo "\$IMAGES" | sort -u | xargs docker rmi -f 2>/dev/null || true
  echo "  Images lama-* supprimées"
else
  echo "  Aucune image lama-* trouvée"
fi

echo ""
echo "=== [5/6] Suppression des volumes Docker lama-* ==="
VOLUMES=\$(docker volume ls --filter "name=lama-" -q 2>/dev/null || true)
if [ -n "\$VOLUMES" ]; then
  echo "\$VOLUMES" | xargs docker volume rm 2>/dev/null || true
  echo "  Volumes supprimés: \$(echo \$VOLUMES | tr '\n' ' ')"
else
  echo "  Aucun volume lama-* trouvé"
fi

echo ""
echo "=== Nettoyage réseaux Docker lama-* ==="
NETWORKS=\$(docker network ls --filter "name=lama-" --format "{{.Name}}" 2>/dev/null | grep -v traefik-net || true)
if [ -n "\$NETWORKS" ]; then
  echo "\$NETWORKS" | xargs docker network rm 2>/dev/null || true
  echo "  Réseaux supprimés"
else
  echo "  Aucun réseau lama-* à supprimer"
fi

echo ""
echo "=== Nettoyage images dangling ==="
docker image prune -f

echo ""
echo "=== [6/6] Suppression des répertoires de déploiement ==="
sudo rm -rf /opt/playalama/prod
sudo rm -rf /opt/playalama/staging
echo "  ✓ /opt/playalama/prod/ supprimé"
echo "  ✓ /opt/playalama/staging/ supprimé"

# Nettoyer aussi l'ancienne structure si elle existe
if [ -d /srv/playalama ]; then
  sudo rm -rf /srv/playalama
  echo "  ✓ /srv/playalama/ supprimé (ancienne structure + code source)"
fi

if [ -d /opt/playalama/git ]; then
  sudo rm -rf /opt/playalama/git
  echo "  ✓ /opt/playalama/git/ supprimé (bare repo git obsolète)"
fi

if [ "\$INCLUDE_TRAEFIK" = "true" ]; then
  echo ""
  echo "=== Suppression Traefik ==="
  cd /opt/playalama/traefik && docker compose down --remove-orphans 2>/dev/null || true
  docker rm -f traefik 2>/dev/null || true
  docker rmi -f traefik 2>/dev/null || true
  docker network rm traefik-net 2>/dev/null || true
  sudo rm -rf /opt/playalama/traefik
  echo "  ✓ Traefik + acme.json supprimés"
fi

echo ""
echo "=== État final des containers ==="
REMAINING=\$(docker ps -a --format "{{.Names}}" 2>/dev/null || true)
if [ -n "\$REMAINING" ]; then
  docker ps -a --format "  {{.Names}} ({{.Status}})"
else
  echo "  Aucun container"
fi

echo ""
echo "=== Espace Docker libéré ==="
docker system df

echo ""
echo "✓ Remise à zéro VPS terminée"
ENDSCRIPT
)

if [[ "$DRY_RUN" == "true" ]]; then
  printf '\033[0;33m[DRY-RUN]\033[0m Le script suivant serait exécuté sur %s :\n\n' "$REMOTE_TARGET"
  echo "$CLEAN_SCRIPT" | sed 's/^/  /'
  echo ""
  ok "Dry-run terminé — rien n'a été modifié"
  exit 0
fi

log "Connexion à $REMOTE_TARGET et exécution du nettoyage..."
ssh "${SSH_ARGS[@]}" "$REMOTE_TARGET" "bash -s" <<<"$CLEAN_SCRIPT"

echo ""
ok "════════════════════════════════════════════════════"
ok " VPS remis à zéro ✓"
ok " Pour réinstaller : make setup-vps SSH_KEY=..."
ok "════════════════════════════════════════════════════"
