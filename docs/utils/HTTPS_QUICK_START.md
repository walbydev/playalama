# 🚀 Guide d'Action Rapide - Activation HTTPS

## Problème identifié
❌ HTTPS (port 443) : Impossible de se connecter à playalama.online
✅ HTTP (port 80) : Fonctionne

## Solutions implémentées

J'ai créé tous les fichiers et scripts nécessaires pour configurer HTTPS sur votre serveur :

### 📁 Fichiers créés

#### Configuration
- **`src/Server/Lama.Server/appsettings.json`** - Configuration ASP.NET Core
- **`src/Server/Lama.Server/appsettings.Production.json`** - Config production
- **`src/Server/Lama.Server/appsettings.Development.json`** - Config développement

#### Nginx
- **`tools/docker/nginx-playalama.conf`** - Configuration proxy inverse HTTPS

#### Docker
- **`Dockerfile`** - Image Docker du serveur
- **`docker-compose.yml`** - Orchestration avec nginx et certificats

#### Scripts
- **`tools/scripts/setup-https.sh`** - Automatisation complète (root required)
- **`tools/scripts/verify-https.sh`** - Vérification de la configuration

#### Documentation
- **`docs/HTTPS_DEPLOYMENT.md`** - Guide complet de déploiement
- **`docs/HTTPS_MIGRATION_PLAN.md`** - Plan détaillé de migration
- **`docs/DOCKER_DEPLOYMENT.md`** - Guide déploiement Docker
- **`.env.example`** - Variables d'environnement

## 🎯 Trois façons de configurer HTTPS

### Option 1 : Automatique (recommandée pour production)

```bash
# Rendre le script exécutable
chmod +x tools/scripts/setup-https.sh

# Exécuter le script d'installation (nécessite root)
sudo tools/scripts/setup-https.sh

# Vérifier la configuration
chmod +x tools/scripts/verify-https.sh
bash tools/scripts/verify-https.sh playalama.online
```

### Option 2 : Manuel pas à pas

Voir le guide complet dans `docs/HTTPS_DEPLOYMENT.md`

Étapes principales :
1. Installer certbot et nginx
2. Copier la configuration nginx
3. Obtenir le certificat Let's Encrypt
4. Configurer le renouvellement automatique
5. Démarrer le serveur Lama

### Option 3 : Docker Compose (meilleur pour la scalabilité)

```bash
# Construire les images
docker-compose build

# Démarrer les services (http + nginx)
docker-compose up -d

# Pour activer Certbot et obtenir les certificats
docker-compose --profile certbot run --rm certbot

# Recharger la configuration
docker-compose restart nginx
```

## 📋 Checklist pré-déploiement

- [ ] Domaine `playalama.online` pointe vers le serveur (vérifier DNS)
- [ ] Ports 80 et 443 accessibles depuis internet
- [ ] .NET 10 SDK installé sur le serveur
- [ ] Accès root ou sudo sur le serveur
- [ ] Email de contact valide pour Let's Encrypt

## 🔍 Verification

Après la configuration, tester :

```bash
# HTTP (devrait rediriger vers HTTPS)
curl -I http://playalama.online

# HTTPS (devrait retourner 200 OK)
curl -I https://playalama.online

# Endpoint santé
curl -s https://playalama.online/health | jq .

# Certificat SSL (devrait afficher la date d'expiration)
echo | openssl s_client -servername playalama.online -connect playalama.online:443 2>/dev/null | openssl x509 -noout -dates
```

## 📞 Troubleshooting

| Problème | Solution |
|----------|----------|
| **HTTPS still not working** | Vérifier: DNS, firewall, nginx status, certificat existence |
| **Certificat expiré** | `sudo certbot renew --verbose` |
| **nginx non démarre** | `sudo nginx -t` pour vérifier la config |
| **Port 443 bloqué** | Vérifier le firewall: `sudo ufw allow 443` |
| **Permission denied** | Les certificats doivent être en `755` sur les répertoires |

Voir la section "Troubleshooting" dans `docs/HTTPS_DEPLOYMENT.md` pour plus de détails.

## 📖 Documentation complète

Tous les détails techniques et les options avancées se trouvent dans :
- **`docs/HTTPS_DEPLOYMENT.md`** - Guide HTTPS complet
- Commentaires dans les fichiers de configuration

## 🎬 Prochaines étapes

1. **Choisir votre méthode** (option 1, 2 ou 3 ci-dessus)
2. **Exécuter la configuration**
3. **Vérifier avec le script** `verify-https.sh`
4. **Monitorer** les certificats (renouvellement auto via certbot)
5. **Tester** vos clients avec HTTPS

---

**Status**: ✅ Configuration HTTPS complète et prête à déployer

