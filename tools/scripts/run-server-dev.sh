#!/bin/bash

# Script pour tester le serveur Lama localement en développement
# Ce script démarre le serveur sans certificat SSL

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "========================================"
echo "🎮 LAMA Server - Mode Développement"
echo "========================================"
echo ""

# Vérifier que .NET est installé
if ! command -v dotnet &>/dev/null; then
    echo "❌ .NET SDK non trouvé. Veuillez installer .NET 10 SDK."
    exit 1
fi

echo "✅ Dépendances détectées"
echo "   .NET version: $(dotnet --version)"
echo ""

# Options
BUILD_ONLY=false
RELEASE_MODE=false

# Parser les arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --build-only)
            BUILD_ONLY=true
            shift
            ;;
        --release)
            RELEASE_MODE=true
            shift
            ;;
        *)
            echo "Options non reconnues: $1"
            exit 1
            ;;
    esac
done

# Déterminer la configuration
CONFIG="Debug"
if [ "$RELEASE_MODE" = true ]; then
    CONFIG="Release"
fi

echo "📦 Compilation du projet..."
dotnet build -c "$CONFIG" "$PROJECT_ROOT/src/Server/Lama.Server/Lama.Server.csproj"

if [ "$BUILD_ONLY" = true ]; then
    echo ""
    echo "✅ Compilation terminée (mode build-only)"
    exit 0
fi

echo ""
echo "========================================"
echo "🚀 Démarrage du serveur"
echo "========================================"
echo ""
echo "Configuration: $CONFIG"
echo ""
echo "Accès:"
echo "  HTTP   : http://localhost:5000"
echo "  Santé  : http://localhost:5000/health"
echo ""
echo "Pour tester:"
echo '  curl -s http://localhost:5000/health | jq'
echo '  curl -s -X POST http://localhost:5000/api/games \'
echo '    -H "Content-Type: application/json" \'
echo '    -d '"'"'{"hostName":"Alice","gameLevel":"Standard"}'"'"
echo ""
echo "Appuyez sur Ctrl+C pour arrêter le serveur"
echo ""
echo "========================================"
echo ""

# Lancer le serveur
cd "$PROJECT_ROOT"
ASPNETCORE_ENVIRONMENT="$CONFIG" dotnet run --project src/Server/Lama.Server --configuration "$CONFIG" --no-build

