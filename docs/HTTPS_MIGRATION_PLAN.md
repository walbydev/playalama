# Plan de Migration HTTPS pour playalama.online

## Contexte

Le serveur LAMA était accessible en HTTP (port 80) mais pas en HTTPS (port 443). Cela pose des risques de sécurité et empêche les clients modernes typés de communiquer de manière sécurisée.

## Analyse du problème

### Observation
```bash
✅ HTTP   : curl -I http://playalama.online → 200 OK (nginx)
❌ HTTPS  : curl -I https://playalama.online → Connection refused
```

### Cause racine
1. Nginx n'écoutait que sur le port 80 (HTTP)
2. Pas de configuration pour le port 443 (HTTPS)
3. Pas de certificat SSL/TLS présent
4. Le serveur ASP.NET Core sous-jacent n'était pas configuré pour HTTPS

## Architecture actuelle (avant migration)

```
Utilisateur
    ↓ (HTTP)
Nginx:80 (proxy inverse)
    ↓
ASP.NET Core:5000 (serveur Lama)
```

## Architecture proposée (après migration)

```
Utilisateur
    ↓ (HTTP)
Nginx:80 → Redirection 301 vers HTTPS
    ↓
Utilisateur
    ↓ (HTTPS)
Nginx:443 (TLS 1.2+)
    ↓ (HTTP local)
ASP.NET Core:5000 (serveur Lama)
```

## Composants de la solution

### 1. Certificat SSL/TLS
- **Provider**: Let's Encrypt (gratuit, automatisé)
- **Outil**: Certbot
- **Chemin**: `/etc/letsencrypt/live/playalama.online/`
- **Renouvellement**: Automatique via systemd timer

### 2. Configuration Nginx
- **Écoute**: 0.0.0.0:80, 0.0.0.0:443
- **Redirection**: HTTP → HTTPS (301 Moved Permanently)
- **Proxy**: HTTPS → HTTP local:5000
- **Headers de sécurité**: HSTS, X-Frame-Options, X-Content-Type-Options

### 3. Configuration ASP.NET Core
- **Port HTTP**: 5000 (écoute locale)
- **Port HTTPS**: 5001 (désactivé, Nginx gère HTTPS)
- **Configuration**: Via `appsettings.Production.json`
- **Certificat**: Non nécessaire sur le serveur (Nginx gère SSL)

### 4. Automatisation
- **Script setup**: `tools/scripts/setup-https.sh` (installation)
- **Script verify**: `tools/scripts/verify-https.sh` (vérification)
- **Docker**: Support multi-conteneurs avec docker-compose

## Plan de migration détaillé

### Phase 1 : Préparation (0.5h)
- [ ] Notifier les utilisateurs
- [ ] Créer une branche git pour les changements
- [ ] Préparer le rollback plan

### Phase 2 : Installation des dépendances (0.5h)
```bash
sudo apt-get update
sudo apt-get install -y certbot python3-certbot-nginx nginx
```

### Phase 3 : Déploiement des fichiers de configuration (0.5h)
- [ ] Copier `tools/docker/nginx-playalama.conf` → `/etc/nginx/sites-available/playalama.online`
- [ ] Activer le site: `ln -s /etc/nginx/sites-available/playalama.online /etc/nginx/sites-enabled/`
- [ ] Vérifier la syntaxe: `sudo nginx -t`
- [ ] Recharger nginx: `sudo systemctl reload nginx`

### Phase 4 : Obtention du certificat SSL (0.5h)
```bash
sudo certbot certonly \
    --nginx \
    --non-interactive \
    --agree-tos \
    --email admin@playalama.online \
    -d playalama.online \
    -d www.playalama.online
```

### Phase 5 : Test et validation (0.5h)
```bash
# Vérifier HTTP → HTTPS redirection
curl -I http://playalama.online

# Vérifier HTTPS
curl -I https://playalama.online

# Vérifier le certificat
echo | openssl s_client -servername playalama.online -connect playalama.online:443 2>/dev/null | openssl x509 -noout -dates

# Vérifier l'endpoint santé
curl -s https://playalama.online/health | jq .
```

### Phase 6 : Monitoring et maintenance (continu)
- [ ] Configurer le renouvellement automatique: `sudo systemctl enable certbot.timer`
- [ ] Monitorer les certificats: `sudo certbot certificates`
- [ ] Logs nginx: `sudo tail -f /var/log/nginx/playalama-error.log`

## Timeline estimée

| Phase | Durée | Status |
|-------|-------|--------|
| Préparation | 30 min | ✅ |
| Installation dépendances | 30 min | ⏳ |
| Configuration nginx | 30 min | ⏳ |
| Certificat SSL | 30 min | ⏳ |
| Test et validation | 30 min | ⏳ |
| **Total** | **2.5 heures** | ⏳ |

## Risques et mitigations

| Risque | Impact | Mitigation |
|--------|--------|-----------|
| Erreur de configuration nginx | Service indisponible | Valider avec `nginx -t`, avoir rollback |
| Certificat non obtenu | HTTPS n'activat pas | Vérifier DNS, retry Certbot |
| Port 443 bloqué/firewall | HTTPS inaccessible | Vérifier règles firewall |
| Certificat expiré | Service indisponible | Renouvellement auto via Certbot |

## Rollback plan

Si des problèmes surviennent :

```bash
# Désactiver HTTPS temporairement
sudo rm /etc/nginx/sites-enabled/playalama.online

# Revenir à une conf HTTP simple
sudo nginx -t
sudo systemctl reload nginx

# Investiguer et corriger
# ...

# Réactiver HTTPS
sudo ln -s /etc/nginx/sites-available/playalama.online \
    /etc/nginx/sites-enabled/playalama.online
sudo nginx -t
sudo systemctl reload nginx
```

## Checklist de validation post-migration

- [ ] HTTP redirige vers HTTPS
- [ ] HTTPS répond avec certificat valide
- [ ] Endpoint `/health` accessible en HTTPS
- [ ] Création de partie possible en HTTPS
- [ ] Notifications SSE fonctionnent en HTTPS
- [ ] Headers de sécurité présents (HSTS, etc.)
- [ ] Certificat se renouvelle automatiquement
- [ ] Logs nginx sans erreurs
- [ ] Performance non dégradée

## Monitoring post-migration

### Certificate expiration
```bash
# Vérifier les certificats
sudo certbot certificates

# Tester le renouvellement
sudo certbot renew --dry-run

# Vérifier la date d'expiration
echo | openssl s_client -servername playalama.online -connect playalama.online:443 2>/dev/null | openssl x509 -noout -dates
```

### Performance
```bash
# Vérifier les connexions SSL
curl -w "@curl-format.txt" -o /dev/null -s https://playalama.online/health

# Logs d'erreurs
sudo tail -f /var/log/nginx/playalama-error.log
```

### Security scanning
- Utiliser SSL Labs: https://www.ssllabs.com/ssltest/analyze.html?d=playalama.online
- Vérifier les headers: https://securityheaders.com/?q=playalama.online

## Documentation supplémentaire

- `docs/HTTPS_DEPLOYMENT.md` - Guide technique complet
- `HTTPS_QUICK_START.md` - Guide d'action rapide
- `tools/docker/nginx-playalama.conf` - Configuration nginx annotée
- `Dockerfile` - Déploiement en conteneur
- `docker-compose.yml` - Orchestration multi-services

## Questions fréquentes

**Q: Quand les certificats se renouvellent-ils?**
R: Automatiquement via le systemd timer `certbot.timer`, 30 jours avant expiration.

**Q: Quel certificat provider utilisez-vous?**
R: Let's Encrypt (gratuit, standard de l'industrie, reconnu par tous les navigateurs).

**Q: Puis-je revenir à HTTP?**
R: Oui, c'est un rollback simple (voir section Rollback plan).

**Q: La performance sera-t-elle affectée?**
R: Non, HTTPS sur nginx a un impact très faible et les performances peuvent même s'améliorer (TLS 1.3, compression).

**Q: Que faire si le certificat expire?**
R: C'est automatique via Certbot. En cas de problème, le renouvellement manuel: `sudo certbot renew --verbose`

## Support et maintenance

Pour toute question ou problème:
1. Consulter `docs/HTTPS_DEPLOYMENT.md`
2. Exécuter le script de vérification: `bash tools/scripts/verify-https.sh`
3. Vérifier les logs: `sudo tail -f /var/log/nginx/playalama-error.log`
4. Consulter la section Troubleshooting

---

**Document créé**: 2026-06-18
**Version**: 1.0
**Status**: Prêt pour déploiement

