#!/usr/bin/env bash
# Quick test: Vérifier que l'endpoint /register est accessible

set -e

echo "🔍 Vérification de l'accès à l'endpoint /register..."
echo ""

# Test 1: Vérifier que le Server écoute sur 5201
echo "1️⃣ Vérifier Server sur 5201..."
if curl -s http://localhost:5201/health >/dev/null 2>&1; then
    echo "   ✅ Server accessible sur http://localhost:5201"
else
    echo "   ❌ Server NON accessible sur port 5201"
    echo "   → Lancer: make option-a-server"
    exit 1
fi

# Test 2: Vérifier que WebApp écoute sur 5202
echo "2️⃣ Vérifier WebApp sur 5202..."
if curl -s http://localhost:5202/ >/dev/null 2>&1; then
    echo "   ✅ WebApp accessible sur http://localhost:5202"
else
    echo "   ❌ WebApp NON accessible sur port 5202"
    echo "   → Lancer: make option-a-webapp"
    exit 1
fi

# Test 3: Vérifier que l'endpoint /api/v1/auth/register existe
echo "3️⃣ Tester l'endpoint /register (POST)..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST http://localhost:5201/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"test_user","password":"password123"}')

HTTP_CODE=$(echo "$RESPONSE" | tail -n 1)
BODY=$(echo "$RESPONSE" | head -n -1)

if [ "$HTTP_CODE" = "400" ] || [ "$HTTP_CODE" = "201" ] || [ "$HTTP_CODE" = "409" ]; then
    echo "   ✅ Endpoint accessible! (HTTP $HTTP_CODE)"
    echo "   Response: $BODY"
else
    echo "   ❌ Endpoint retourne HTTP $HTTP_CODE (attendu: 201|400|409)"
    echo "   Response: $BODY"
    exit 1
fi

echo ""
echo "✅ Tous les tests passent!"
echo ""
echo "🎯 Prochaines étapes:"
echo "   1. Ouvrir: http://localhost:5202"
echo "   2. Cliquer: S'inscrire"
echo "   3. Remplir: Pseudo, Mot de passe"
echo "   4. Soumettre"
echo "   5. Vérifier: Pas de 404, redirection vers /games ✅"
echo ""

