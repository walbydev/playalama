#!/bin/bash

# Script de vérification de la configuration HTTPS pour playalama.online

set -e

DOMAIN="${1:-playalama.online}"
COLORS=true

# Fonctions de couleur
red() {
    if [ "$COLORS" = true ]; then
        echo -e "\033[31m$1\033[0m"
    else
        echo "$1"
    fi
}

green() {
    if [ "$COLORS" = true ]; then
        echo -e "\033[32m$1\033[0m"
    else
        echo "$1"
    fi
}

yellow() {
    if [ "$COLORS" = true ]; then
        echo -e "\033[33m$1\033[0m"
    else
        echo "$1"
    fi
}

blue() {
    if [ "$COLORS" = true ]; then
        echo -e "\033[34m$1\033[0m"
    else
        echo "$1"
    fi
}

# Vérifications
echo "========================================"
blue "Vérification de la configuration HTTPS pour $DOMAIN"
echo "========================================"
echo ""

# Test 1: Connectivité DNS
echo "Test 1: Résolution DNS"
if nslookup "$DOMAIN" &>/dev/null; then
    green "✓ DNS résout $DOMAIN"
else
    red "✗ DNS ne résout pas $DOMAIN"
fi
echo ""

# Test 2: Accès HTTP
echo "Test 2: Accès HTTP"
if curl -s -I "http://$DOMAIN" | head -1 | grep -q "200\|301\|302"; then
    green "✓ HTTP accessible"
else
    red "✗ HTTP non accessible"
fi
echo ""

# Test 3: Accès HTTPS
echo "Test 3: Accès HTTPS"
if curl -s -I "https://$DOMAIN" >/dev/null 2>&1; then
    green "✓ HTTPS accessible"
    HTTPS_OK=true
else
    red "✗ HTTPS non accessible"
    HTTPS_OK=false
fi
echo ""

# Test 4: Endpoint santé
echo "Test 4: Endpoint /health"
if HEALTH=$(curl -s "https://$DOMAIN/health" 2>/dev/null); then
    green "✓ Endpoint /health répond"
    if echo "$HEALTH" | grep -q '"status"'; then
        green "  Réponse santé: $HEALTH"
    fi
else
    red "✗ Endpoint /health n'est pas accessible"
fi
echo ""

# Test 5: Certificat SSL
echo "Test 5: Certificat SSL"
if command -v openssl &>/dev/null; then
    CERT_INFO=$(echo "" | openssl s_client -servername "$DOMAIN" -connect "$DOMAIN:443" 2>/dev/null | openssl x509 -noout -dates 2>/dev/null)
    if [ -n "$CERT_INFO" ]; then
        green "✓ Certificat SSL valide"
        echo "$CERT_INFO" | while read line; do
            echo "  $line"
        done
    else
        red "✗ Impossible de vérifier le certificat SSL"
    fi
else
    yellow "⚠ OpenSSL non trouvé, vérification du certificat omise"
fi
echo ""

# Test 6: Headers de sécurité
echo "Test 6: Headers de sécurité"
HEADERS=$(curl -s -I "https://$DOMAIN" 2>/dev/null)
if echo "$HEADERS" | grep -q "Strict-Transport-Security"; then
    green "✓ HSTS configuré"
fi
if echo "$HEADERS" | grep -q "X-Content-Type-Options"; then
    green "✓ X-Content-Type-Options configuré"
fi
if echo "$HEADERS" | grep -q "X-Frame-Options"; then
    green "✓ X-Frame-Options configuré"
fi
echo ""

# Test 7: Redirection HTTP vers HTTPS
echo "Test 7: Redirection HTTP vers HTTPS"
REDIRECT=$(curl -s -I "http://$DOMAIN/test" 2>/dev/null | grep -i "location.*https" || true)
if [ -n "$REDIRECT" ]; then
    green "✓ HTTP redirige vers HTTPS"
else
    yellow "⚠ Redirection HTTP vers HTTPS non vérifiée"
fi
echo ""

# Test 8: Nginx status
if command -v systemctl &>/dev/null; then
    echo "Test 8: Statut nginx"
    if systemctl is-active --quiet nginx; then
        green "✓ Service nginx lancé"
    else
        red "✗ Service nginx non lancé"
    fi
    echo ""
fi

# Résumé
echo "========================================"
if [ "$HTTPS_OK" = true ]; then
    green "✅ Configuration HTTPS active et fonctionnelle"
else
    yellow "⚠ Configuration HTTPS à vérifier"
fi
echo "========================================"
echo ""

# Commandes utiles
echo "Commandes utiles :"
echo "  Vérifier les logs nginx   : sudo tail -f /var/log/nginx/playalama-error.log"
echo "  Vérifier status nginx     : sudo systemctl status nginx"
echo "  Recharger nginx           : sudo systemctl reload nginx"
echo "  Certificat actuel         : echo | openssl s_client -servername $DOMAIN -connect $DOMAIN:443 2>/dev/null | openssl x509 -noout -dates"
echo "  Renouveler certificat     : sudo certbot renew --verbose"
echo ""

