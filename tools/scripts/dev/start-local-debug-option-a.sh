#!/usr/bin/env bash
set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/tools/docker/docker-compose.local-debug.yml"

# Couleurs pour l'output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Lama Local Debug - Option A (Docker + Native .NET)${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""

# Démarrage des services Docker
echo -e "${YELLOW}📦 Démarrage de PostgreSQL sur le port 5200...${NC}"
cd "$PROJECT_ROOT"
docker compose -f "$COMPOSE_FILE" up -d
sleep 3
docker compose -f "$COMPOSE_FILE" ps

echo ""
echo -e "${GREEN}✓ PostgreSQL est prêt sur localhost:5200${NC}"
echo ""

# Affichage de la topologie
echo -e "${BLUE}📊 Topologie des services:${NC}"
echo "  • PostgreSQL:    localhost:5200"
echo "  • Server:        localhost:5201 (à lancer: dotnet run --project src/apps/Lama.Server)"
echo "  • WebApp:        localhost:5202 (a lancer: dotnet run --project src/apps/Lama.WebApp)"
echo ""

echo -e "${BLUE}📝 Prochaines étapes:${NC}"
echo "  1. Dans un terminal: cd $PROJECT_ROOT && dotnet run --project src/apps/Lama.Server"
echo "  2. Dans un autre:    cd $PROJECT_ROOT && dotnet run --project src/apps/Lama.WebApp"
echo "  3. Ouvrir: http://localhost:5202 dans le navigateur"
echo ""

echo -e "${YELLOW}🛑 Pour arrêter PostgreSQL:${NC}"
echo "   docker compose -f $COMPOSE_FILE down"
echo ""

echo -e "${GREEN}✓ Configuration prête!${NC}"

