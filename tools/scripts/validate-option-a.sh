#!/usr/bin/env bash
# Validation Checklist - Option A Setup

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${BLUE}"
cat << "EOF"
╔════════════════════════════════════════════════════════════╗
║       Lama v1.1.0 - Option A Setup Validation              ║
║                   Configuration Check                      ║
╚════════════════════════════════════════════════════════════╝
EOF
echo -e "${NC}"

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../" && pwd)"
FAIL_COUNT=0
PASS_COUNT=0

# Helper functions
check_file() {
    local file=$1
    local description=$2
    if [[ -f "$PROJECT_ROOT/$file" ]]; then
        echo -e "${GREEN}✓${NC} $description"
        ((PASS_COUNT++))
    else
        echo -e "${RED}✗${NC} MISSING: $description"
        echo -e "  Expected: $PROJECT_ROOT/$file"
        ((FAIL_COUNT++))
    fi
}

check_docker_compose() {
    local file=$1
    local description=$2
    local full_path="$PROJECT_ROOT/$file"
    if [[ -f "$full_path" ]] && docker-compose -f "$full_path" config >/dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} Valid Docker Compose: $description"
        ((PASS_COUNT++))
    else
        echo -e "${RED}✗${NC} Invalid Docker Compose: $description"
        ((FAIL_COUNT++))
    fi
}

check_contains() {
    local file=$1
    local pattern=$2
    local description=$3
    if grep -q "$pattern" "$PROJECT_ROOT/$file"; then
        echo -e "${GREEN}✓${NC} $description"
        ((PASS_COUNT++))
    else
        echo -e "${RED}✗${NC} MISSING: $description"
        echo -e "  Pattern: $pattern"
        echo -e "  File: $file"
        ((FAIL_COUNT++))
    fi
}

# ═══════════════════════════════════════════════════════════
echo -e "${YELLOW}📦 Configuration Files${NC}"
echo "─────────────────────────────────────────────────"

check_file "docker-compose.local-debug-option-a.yml" "Docker Compose (Option A)" || true
check_file "src/Server/Lama.Server/Properties/launchSettings.json" "Server launchSettings"
check_file "src/Web/Lama.WebApp/Properties/launchSettings.json" "WebApp launchSettings"
check_file ".env.example" "Environment variables template"

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${YELLOW}📋 Documentation${NC}"
echo "─────────────────────────────────────────────────"

check_file "OPTION_A_SETUP.md" "OPTION A Setup guide"
check_file "docs/utils/OPTION_A_QUICKSTART.md" "Quick start guide"
check_file "docs/utils/OPTION_A_LOCAL_DEBUG.md" "Detailed debug guide"
check_file "docs/utils/INDEX.md" "Utils documentation index"

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${YELLOW}🔧 Scripts${NC}"
echo "─────────────────────────────────────────────────"

check_file "tools/scripts/start-local-debug-option-a.sh" "Startup script"
check_file "tools/scripts/test-option-a.sh" "Test validation script"

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${YELLOW}⚙️  Port Configuration${NC}"
echo "─────────────────────────────────────────────────"

check_contains "src/Server/Lama.Server/Properties/launchSettings.json" "5201" "Server port 5201"
check_contains "src/Web/Lama.WebApp/Properties/launchSettings.json" "5202" "WebApp port 5202"
check_contains "docker-compose.local-debug-option-a.yml" "5200:5432" "PostgreSQL port 5200"

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${YELLOW}🗄️  Database Configuration${NC}"
echo "─────────────────────────────────────────────────"

check_contains "src/Server/Lama.Server/appsettings.Development.json" "Port=5200" "DB connection port 5200"
check_contains "src/Server/Lama.Server/appsettings.Development.json" "AutoMigrate.*true" "Auto-migration enabled"
check_contains "src/Server/Lama.Server/Data/LamaDbContextFactory.cs" "Port=5200" "Factory port 5200"

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${YELLOW}🔗 Service Integration${NC}"
echo "─────────────────────────────────────────────────"

check_contains "src/Web/Lama.WebApp/Properties/launchSettings.json" "5201" "WebApp → Server URL"
check_docker_compose "docker-compose.local-debug-option-a.yml" "Option A Docker Compose"

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${YELLOW}📊 Build Status${NC}"
echo "─────────────────────────────────────────────────"

if cd "$PROJECT_ROOT" && dotnet build src/Server/Lama.Server -c Debug -q 2>/dev/null; then
    echo -e "${GREEN}✓${NC} Lama.Server builds successfully"
    ((PASS_COUNT++))
else
    echo -e "${RED}✗${NC} Lama.Server build failed"
    ((FAIL_COUNT++))
fi

if cd "$PROJECT_ROOT" && dotnet build src/Web/Lama.WebApp -c Debug -q 2>/dev/null; then
    echo -e "${GREEN}✓${NC} Lama.WebApp builds successfully"
    ((PASS_COUNT++))
else
    echo -e "${RED}✗${NC} Lama.WebApp build failed"
    ((FAIL_COUNT++))
fi

# ═══════════════════════════════════════════════════════════
echo ""
echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
if [[ $FAIL_COUNT -eq 0 ]]; then
    echo -e "${BLUE}║${NC} ${GREEN}✓ All Checks Passed!${NC} Ready for development.         ${BLUE}║${NC}"
    echo -e "${BLUE}║${NC}                                                        ${BLUE}║${NC}"
    echo -e "${BLUE}║${NC} ${YELLOW}Next Steps:${NC}                                          ${BLUE}║${NC}"
    echo -e "${BLUE}║${NC}   1. make option-a-start   # PostgreSQL Docker        ${BLUE}║${NC}"
    echo -e "${BLUE}║${NC}   2. make option-a-server  # Server on 5201          ${BLUE}║${NC}"
    echo -e "${BLUE}║${NC}   3. make option-a-webapp  # WebApp on 5202          ${BLUE}║${NC}"
    echo -e "${BLUE}║${NC}   4. http://localhost:5202                           ${BLUE}║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
else
    echo -e "${BLUE}║${NC} ${RED}✗ $FAIL_COUNT check(s) failed.${NC} Please review above. ${BLUE}║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
    exit 1
fi

echo ""
echo -e "${YELLOW}Summary:${NC}"
echo "  Passed: $PASS_COUNT"
echo "  Failed: $FAIL_COUNT"
echo ""

