#!/usr/bin/env bash
# setup-alias.sh
# Configure l'alias 'lama' pour le projet LAMA
# À sourcer depuis votre shell ou ~/.bashrc

set -euo pipefail

# Déterminer le répertoire racine du projet
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONSOLE_PROJECT="$PROJECT_ROOT/src/apps/Lama.Console/Lama.Console.csproj"

# Créer l'alias lama
# Cet alias :
# - Charge les variables d'environnement du projet si elles existent
# - Exécute dotnet run dans le bon répertoire
# - Transmet tous les arguments à l'application
alias lama="dotnet run --project '$CONSOLE_PROJECT' --"

# Variable d'environnement pour les sessions
export LAMA_PROJECT_ROOT="$PROJECT_ROOT"
export LAMA_CONSOLE_PROJECT="$CONSOLE_PROJECT"

# Afficher un message de confirmation
echo "✓ Alias 'lama' activé"
echo "  Racine du projet: $PROJECT_ROOT"
echo "  Projet console: $CONSOLE_PROJECT"
echo ""
echo "Utilisation:"
echo "  lama                        # Mode interactif"
echo "  lama interactive            # Mode interactif (explicite)"
echo "  lama game create Alice      # Créer une partie"
echo "  lama play move H8 LAMA H    # Poser un mot"

