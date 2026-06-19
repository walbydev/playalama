#!/usr/bin/env bash
# Test complet Option A : PostgreSQL Docker + Services natifs locaux

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/docker-compose.local-debug-option-a.yml"

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Test Option A: PostgreSQL Docker + Native Services${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""

# Check if PostgreSQL is running
echo -e "${YELLOW}🔍 Vérification de PostgreSQL sur 5200...${NC}"
if docker exec postgres-lama-option-a pg_isready -U lama_dev -d lama_dev >/dev/null 2>&1; then
    echo -e "${GREEN}✓ PostgreSQL est en cours d'exécution${NC}"
else
    echo -e "${RED}✗ PostgreSQL n'est pas disponible${NC}"
    echo -e "${YELLOW}  Lancer: docker-compose -f $COMPOSE_FILE up -d${NC}"
    exit 1
fi

echo ""
echo -e "${YELLOW}🔍 Vérification de la connectivité PostgreSQL...${NC}"

# Test connection via psql
if docker exec postgres-lama-option-a psql -U lama_dev -d lama_dev -c "SELECT 1" >/dev/null 2>&1; then
    echo -e "${GREEN}✓ Connexion PostgreSQL réussie${NC}"
else
    echo -e "${RED}✗ Impossible de se connecter à PostgreSQL${NC}"
    exit 1
fi

echo ""
echo -e "${YELLOW}🔍 Vérification des tables de schéma...${NC}"

# Vérifier que les colonnes d'auth existent
if docker exec postgres-lama-option-a psql -U lama_dev -d lama_dev -c "SELECT email, password_hash FROM players LIMIT 1" >/dev/null 2>&1; then
    echo -e "${GREEN}✓ Schéma auth est present (email, password_hash)${NC}"
else
    # Il est possible que la table players soit vide, c'est OK
    if docker exec postgres-lama-option-a psql -U lama_dev -d lama_dev -c "\d players" | grep -q "email\|password_hash"; then
        echo -e "${GREEN}✓ Colonnes auth trouvées${NC}"
    else
        echo -e "${YELLOW}⚠ Schéma auth non migré (c'est normal si base neuve)${NC}"
        echo -e "${YELLOW}  Le Server appliquera auto-migrate au démarrage${NC}"
    fi
fi

echo ""
echo -e "${YELLOW}🔍 Vérification des ports...${NC}"

# Ports
PORTS=(5200 5201 5202)
for port in "${PORTS[@]}"; do
    if nc -z 127.0.0.1 "$port" 2>/dev/null; then
        echo -e "${GREEN}✓ Port $port accessible${NC}"
    else
        echo -e "${YELLOW}  Port $port libre (service pas lancé)${NC}"
    fi
done

echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Option A est prête pour le debug!${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${YELLOW}Prochaines étapes:${NC}"
echo "  1️⃣  Terminal 1: cd $PROJECT_ROOT && dotnet run --project src/Server/Lama.Server"
echo "  2️⃣  Terminal 2: cd $PROJECT_ROOT && dotnet run --project src/Web/Lama.WebApp"
echo "  3️⃣  Navigateur: http://localhost:5202"
echo ""
echo -e "${YELLOW}Commandes utiles:${NC}"
echo "  make option-a-stop        # Arrêter PostgreSQL"
echo "  make option-a-clean       # Réinitialiser DB"
echo "  make health-option-a      # Vérifier l'état des services"
echo ""

