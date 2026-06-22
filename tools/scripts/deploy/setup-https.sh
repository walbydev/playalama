#!/bin/bash

# Script de configuration HTTPS pour playalama.online
# Ce script installe et configure Let's Encrypt, nginx et le serveur Lama

set -e

DOMAIN="playalama.online"
LAMA_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$(dirname "$LAMA_SCRIPT_DIR")")")"
NGINX_CONF="/etc/nginx/sites-available/playalama.online"
NGINX_CONF_SRC="$LAMA_SCRIPT_DIR/../../docker/nginx-playalama.conf"

# Vérifier les droits root
if [[ $EUID -ne 0 ]]; then
    echo "Ce script doit être exécuté en tant que root."
    exit 1
fi

echo "========================================"
echo "Configuration HTTPS pour $DOMAIN"
echo "========================================"

# Étape 1 : Installer certbot et les dépendances
echo "📦 Installation des dépendances..."
apt-get update
apt-get install -y certbot python3-certbot-nginx nginx

# Étape 2 : Configurer nginx comme proxy inverse (HTTP d'abord)
echo "🔧 Configuration nginx..."
cp "$NGINX_CONF_SRC" "$NGINX_CONF"

# Activer le site
ln -sf "$NGINX_CONF" /etc/nginx/sites-enabled/playalama.online

# Tester la configuration nginx
nginx -t

# Recharger nginx
systemctl reload nginx

# Étape 3 : Obtenir le certificat Let's Encrypt
echo "🔒 Obtention du certificat Let's Encrypt pour $DOMAIN..."
certbot certonly \
    --nginx \
    --non-interactive \
    --agree-tos \
    --email admin@$DOMAIN \
    -d $DOMAIN \
    -d www.$DOMAIN

# Étape 4 : Configurer le renouvellement automatique
echo "⏰ Configuration du renouvellement automatique..."
systemctl enable certbot.timer
systemctl start certbot.timer

# Étape 5 : Recharger nginx avec la configuration HTTPS
echo "🔄 Rechargement de nginx avec configuration HTTPS..."
nginx -t
systemctl reload nginx

# Étape 6 : Fix des permissions pour le certificat
echo "🔐 Configuration des permissions..."
chmod 755 /etc/letsencrypt/live/$DOMAIN
chmod 755 /etc/letsencrypt/archive/$DOMAIN

# Étape 7 : Vérifier la configuration
echo "✅ Vérification de la configuration..."
echo ""
echo "Vérification HTTP :"
curl -I http://$DOMAIN 2>&1 | head -5
echo ""
echo "Vérification HTTPS :"
curl -I https://$DOMAIN 2>&1 | head -5
echo ""

echo "========================================"
echo "✅ Configuration HTTPS terminée !"
echo "========================================"
echo ""
echo "Étapes suivantes :"
echo "1. Démarrer le serveur Lama :"
echo "   dotnet run --project $PROJECT_ROOT/src/apps/Lama.Server"
echo ""
echo "2. Vérifier la configuration :"
echo "   curl -s https://$DOMAIN/health | jq"
echo ""
echo "3. Pour les renouvellements manuels :"
echo "   certbot renew --dry-run"
echo ""

