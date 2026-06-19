# Guide de Déploiement HTTPS pour playalama.online

## Résumé du problème

Le serveur `playalama.online` était accessible en HTTP (port 80) mais pas en HTTPS (port 443). Cela empêchait les clients de se connecter de manière sécurisée.

## Solution implémentée

### 1. Configuration nginx avec proxy inverse
- Redirection HTTP → HTTPS
- Proxy inverse vers le serveur Lama (port 5000/5001)
- Support des Server-Sent Events (SSE) pour les mises à jour en temps réel
- Headers de sécurité modernes (HSTS, CSP, etc.)

**Fichier** : `tools/docker/nginx-playalama.conf`

### 2. Configuration ASP.NET Core
- Support natif d'HTTPS via Kestrel
- Lecture de la configuration depuis `appsettings.json`
- Point de terminaison HTTP sur le port 5000
- Point de terminaison HTTPS sur le port 5001

**Fichier** : `src/Server/Lama.Server/appsettings.json`

### 3. Scripts d'automatisation
- `tools/scripts/setup-https.sh` : Script complet de configuration (nécessite root)

## Installation manuelle pas à pas

### Prérequis
- Accès au serveur (`playalama.online`)
- Droits root ou sudo
- Domaine configuré correctement dans le DNS

### Étape 1 : Installer les dépendances

```bash
sudo apt-get update
sudo apt-get install -y certbot python3-certbot-nginx nginx
```

### Étape 2 : Configurer nginx

```bash
# Copier la configuration
sudo cp tools/docker/nginx-playalama.conf /etc/nginx/sites-available/playalama.online

# Activer le site
sudo ln -sf /etc/nginx/sites-available/playalama.online /etc/nginx/sites-enabled/

# Tester la configuration
sudo nginx -t

# Recharger nginx
sudo systemctl reload nginx
```

### Étape 3 : Obtenir le certificat SSL/TLS

```bash
sudo certbot certonly \
    --nginx \
    --non-interactive \
    --agree-tos \
    --email admin@playalama.online \
    -d playalama.online \
    -d www.playalama.online
```

Les fichiers du certificat seront situés à :
- `/etc/letsencrypt/live/playalama.online/fullchain.pem`
- `/etc/letsencrypt/live/playalama.online/privkey.pem`

### Étape 4 : Configurer le renouvellement automatique

```bash
sudo systemctl enable certbot.timer
sudo systemctl start certbot.timer

# Vérifier l'état
sudo systemctl status certbot.timer
```

### Étape 5 : Corriger les permissions

```bash
sudo chmod 755 /etc/letsencrypt/live/playalama.online
sudo chmod 755 /etc/letsencrypt/archive/playalama.online
sudo chmod 644 /etc/letsencrypt/live/playalama.online/privkey.pem
```

### Étape 6 : Démarrer le serveur Lama

```bash
# Mode développement local
cd /path/to/Lama
dotnet run --project src/Server/Lama.Server

# Ou en tant que service systemd (voir section ci-dessous)
```

### Étape 7 : Tester

```bash
# Test HTTP (redirection vers HTTPS)
curl -I http://playalama.online

# Test HTTPS
curl -I https://playalama.online

# Test de l'API santé
curl -s https://playalama.online/health | jq
```

## Configuration systemd (optionnel)

Créez un service systemd pour démarrer automatiquement le serveur Lama :

**Fichier** : `/etc/systemd/system/lama-server.service`

```ini
[Unit]
Description=LAMA Online Game Server
After=network.target

[Service]
Type=simple
User=lama
WorkingDirectory=/home/lama/RiderProjects/Games/Lama
ExecStart=/usr/bin/dotnet run --project src/Server/Lama.Server --configuration Release
Restart=always
RestartSec=10
StandardOutput=append:/var/log/lama-server/output.log
StandardError=append:/var/log/lama-server/error.log

[Install]
WantedBy=multi-user.target
```

Activer le service :

```bash
sudo systemctl daemon-reload
sudo systemctl enable lama-server
sudo systemctl start lama-server
sudo systemctl status lama-server
```

## Automatisation avec le script

Pour automatiser l'ensemble du processus :

```bash
chmod +x tools/scripts/setup-https.sh
sudo tools/scripts/setup-https.sh
```

## Troubleshooting

### HTTPS n'accède toujours pas

```bash
# Vérifier la configuration nginx
sudo nginx -t

# Vérifier que les certificats existent
ls -la /etc/letsencrypt/live/playalama.online/

# Vérifier les logs nginx
sudo tail -f /var/log/nginx/error.log

# Vérifier que le serveur Lama écoute
sudo netstat -tulpn | grep :5000
```

### Certificat expiré

```bash
# Renouveler manuellement
sudo certbot renew --verbose

# Recharger nginx
sudo systemctl reload nginx
```

### Permissions refusées

```bash
# Vérifier les permissions du fichier clé privée
ls -la /etc/letsencrypt/archive/playalama.online/

# Les permissions doivent être 644 ou 755 sur les répertoires
sudo chmod 755 /etc/letsencrypt/live/playalama.online
sudo chmod 755 /etc/letsencrypt/archive/playalama.online
```

## Vérification de la sécurité

### Scan SSL/TLS

```bash
# Utiliser ssllabs.com ou le script ci-dessous
curl -s https://api.ssllabs.com/api/v3/analyze?host=playalama.online&publish=off&all=done
```

### Vérification des headers

```bash
curl -I https://playalama.online | grep -i "strict-transport-security\|x-content-type-options\|x-frame-options"
```

## Monitoring

### Vérifier l'état des certificats

```bash
sudo certbot certificates

# Voir la date d'expiration
echo | openssl s_client -servername playalama.online -connect playalama.online:443 2>/dev/null | openssl x509 -noout -dates
```

### Logs d'erreurs

```bash
# Logs nginx
sudo tail -f /var/log/nginx/playalama-error.log

# Logs du serveur Lama (si systemd)
sudo journalctl -u lama-server -f

# Ou les fichiers de log standards
cat /var/log/lama-server/error.log
```

## Rollback

Si vous avez des problèmes, vous pouvez revenir à HTTP temporairement :

```bash
# Désactiver le site HTTPS
sudo rm /etc/nginx/sites-enabled/playalama.online

# Ou utiliser une configuration HTTP simple pour debugging
# Puis recharger nginx
sudo systemctl reload nginx
```

## References

- [Let's Encrypt](https://letsencrypt.org/)
- [Certbot documentation](https://certbot.eff.org/)
- [ASP.NET Core HTTPS](https://docs.microsoft.com/en-us/aspnet/core/security/https)
- [Nginx reverse proxy](https://nginx.org/en/docs/http/ngx_http_proxy_module.html)
- [Security headers](https://securityheaders.com/)

