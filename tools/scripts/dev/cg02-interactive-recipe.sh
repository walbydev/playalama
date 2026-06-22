#!/bin/bash

# Script de recette interactive manuelle pour CG-02
# Simule un parcours complet en mode interactif :
# 1. Créer une partie avec Alice (hôte)
# 2. Rejoindre avec Bob
# 3. Jouer un coup avec Alice
# 4. Passer le tour
# 5. Afficher le plateau et scores
# 6. Terminer la partie

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$(dirname "$SCRIPT_DIR")")")"

echo "═══════════════════════════════════════════"
echo "CG-02 Recette Interactive Manuelle"
echo "═══════════════════════════════════════════"
echo ""

# Créer une session de test
export LAMA_SESSION_DIR="/tmp/lama_cg02_test_$$"
mkdir -p "$LAMA_SESSION_DIR"

cleanup() {
    echo ""
    echo "Nettoyage des fichiers de test..."
    rm -rf "$LAMA_SESSION_DIR"
}

trap cleanup EXIT

echo "[1/5] Créer une partie (hôte Alice)"
echo "Nouvelle partie" | \
(timeout 5 dotnet run --project "$PROJECT_ROOT/src/apps/Lama.Console" -- 2>&1 || true) | head -20

echo ""
echo "[2/5] Rejoindre la partie (invité Bob)"
echo "Note: Cette étape est effectuée manuellement"
echo "  Commande: dotnet run --project src/apps/Lama.Console -- game join <game-id> Bob"

echo ""
echo "[3/5] Jouer un coup (move)"
echo "Note: Cette étape est effectuée manuellement"
echo "  Commande: dotnet run --project src/apps/Lama.Console -- play move H8 LAMA H"

echo ""
echo "[4/5] Afficher le plateau"
echo "Note: Cette étape est effectuée manuellement"
echo "  Commande: dotnet run --project src/apps/Lama.Console -- show board"

echo ""
echo "[5/5] Terminer la partie"
echo "Note: Cette étape est effectuée manuellement"
echo "  Commande: dotnet run --project src/apps/Lama.Console -- game end"

echo ""
echo "═══════════════════════════════════════════"
echo "Instructions de recette manuelle complète:"
echo "═══════════════════════════════════════════"
echo ""
echo "1. Lancer le mode interactif:"
echo "   \$ cd $PROJECT_ROOT"
echo "   \$ export LAMA_SESSION_DIR=/tmp/lama_cg02_test"
echo "   \$ mkdir -p \$LAMA_SESSION_DIR"
echo "   \$ dotnet run --project src/apps/Lama.Console"
echo ""
echo "2. Menu principal: Sélectionner 'Nouvelle partie'"
echo "   → Entrer le nom d'hôte: Alice"
echo "   → Vérifier la création de partie"
echo ""
echo "3. Menu principal: Sélectionner 'Jouer un tour'"
echo "   → Jouer un mot: H8 LAMA H"
echo "   → Vérifier le plateau affiché"
echo "   → Vérifier le rack et scores affichés"
echo ""
echo "4. Menu principal: Sélectionner 'Options'"
echo "   → Afficher la session locale (vérifier GameId, PlayerId)"
echo "   → Retour au menu"
echo ""
echo "5. Menu principal: Sélectionner 'Reafficher le dashboard'"
echo "   → Vérifier que le plateau, rack et scores s'affichent"
echo ""
echo "6. Menu principal: Sélectionner 'Quitter'"
echo "   → Quitter le mode interactif"
echo ""
echo "═══════════════════════════════════════════"
echo "Validation de CG-02:"
echo "═══════════════════════════════════════════"
echo ""
echo "Points de validation:"
echo "  ✓ Menu principal accessible et stable"
echo "  ✓ Création de partie sans erreur"
echo "  ✓ Boucle de tour disponible et stable"
echo "  ✓ Affichage board/rack/scores après action"
echo "  ✓ Session persiste correctement"
echo "  ✓ Quitter sans erreur fatale"
echo ""
echo "If all checks pass => CG-02 CLOSED ✅"
echo ""

