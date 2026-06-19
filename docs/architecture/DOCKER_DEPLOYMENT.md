# Déploiement LAMA avec Docker et Docker Compose

## Vue d'ensemble

Ce guide explique comment déployer le serveur LAMA avec Docker et nginx via docker-compose.

## Prérequis

- Docker (20.10+)
- Docker Compose (v2.0+)
- 2GB RAM minimum
- Port 80 et 443 disponibles

## Architecture

```
┌─────────────────┐
│     Internet    │
└────────┬────────┘
         │
    ┌────▼────┐
    │  Nginx  │ (container)
    │ :80:443 │
    └────┬────┘
         │
    ┌────▼──────────────┐
    │  Lama.Server      │ (container)
    │  .NET 10 + Kestrel│
    │    :5000          │
    └───────────────────┘
```

## Installation rapide

### Étape 1 : Construire les images

```bash
docker-compose build
```

### Étape 2 : Démarrer les services

```bash
# Mode foreground (voir les logs)
docker-compose up

# Ou en arrière-plan
docker-compose up -d
```

### Étape 3 : Obtenir le certificat SSL (si existant)

Let's Encrypt en Docker le mieux est fait hors conteneur:

```bash
# Sur la machine hôte :
sudo certbot certonly \
    --standalone \
    -d playalama.online \
    -d www.playalama.online
```

Puis montez le répertoire des certificats:

```bash
# Mettre à jour docker-compose.yml:
volumes:
  - /etc/letsencrypt:/etc/letsencrypt:ro
```

### Étape 4 : Vérifier

```bash
# Vérifier l'état
docker-compose ps

# Logs du serveur
docker-compose logs -f lama-server

# Logs nginx
docker-compose logs -f nginx

# Test HTTP
curl http://localhost

# Test HTTPS (avec certificats montés)
curl https://localhost -k

# API santé
curl http://localhost/health
```

## Commandes utiles

### Management des conteneurs

```bash
# Démarrer les services
docker-compose start

# Arrêter les services
docker-compose stop

# Redémarrer
docker-compose restart

# Supprimer les conteneurs (garder les volumes)
docker-compose down

# Supprimer tout (conteneurs + volumes)
docker-compose down -v

# Vérifier l'état
docker-compose ps
```

### Logs et debugging

```bash
# Tous les logs
docker-compose logs

# Logs d'un service spécifique
docker-compose logs lama-server
docker-compose logs nginx

# Logs en temps réel
docker-compose logs -f

# Dernières 50 lignes
docker-compose logs --tail=50
```

### Exécution de commandes

```bash
# Shell dans le conteneur serveur
docker-compose exec lama-server bash

# Shell dans le conteneur nginx
docker-compose exec nginx sh

# Tester l'API depuis le conteneur
docker-compose exec lama-server curl http://localhost:5000/health
```

## Configuration avancée

### Variables d'environnement

Créez un fichier `.env`:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
LAMA_SERVER_ALLOW_SHUTDOWN=false
NGINX_DOMAIN=playalama.online
```

Utilisez-le:

```bash
docker-compose --env-file .env up
```

### Volumes personnalisés

```bash
# Monter des assets personnalisés
volumes:
  - ./assets/languages:/app/assets/languages:ro
  - ./logs:/app/logs
```

### Networking

Pour communiquer entre conteneurs:

- Nom d'hôte: `lama-server` (au lieu de localhost)
- Réseau: `lama-network` (créé automatiquement)

```bash
# Nginx vers Lama
proxy_pass http://lama-server:5000;
```

## Certificats SSL en Docker

### Option 1 : Let's Encrypt hors conteneur

Le plus simple et recommandé:

```bash
# Sur la machine hôte
sudo certbot certonly --standalone -d playalama.online

# Dans docker-compose.yml
volumes:
  - /etc/letsencrypt:/etc/letsencrypt:ro
```

### Option 2 : Certbot en conteneur

Si vous voulez automatiser:

```bash
# Profiler Certbot
docker-compose --profile certbot up certbot

# Ou renouvellement automatique
services:
  certbot-renew:
    image: certbot/certbot
    volumes:
      - /etc/letsencrypt:/etc/letsencrypt
    entrypoint: /bin/sh -c "trap exit TERM; while :; do certbot renew --quiet; sleep 12h & wait $!; done"
```

### Option 3 : Auto-signed (développement uniquement)

```bash
# Générer certificat de test
openssl req -x509 -newkey rsa:4096 -nodes \
    -keyout key.pem -out cert.pem -days 365 \
    -subj "/CN=localhost"

# Monter dans le conteneur
volumes:
  - ./cert.pem:/etc/nginx/ssl/cert.pem:ro
  - ./key.pem:/etc/nginx/ssl/key.pem:ro
```

## Performance et scalabilité

### Tuning Nginx

```nginx
worker_processes auto;
worker_connections 4096;

upstream lama_backend {
    keepalive 32;
    server lama-server:5000;
}
```

### Tuning Kestrel

```json
{
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 10485760,
      "MaxRequestHeadersTotalSize": 32768,
      "RequestHeadersTimeout": "00:00:30",
      "KeepAliveTimeout": "00:02:00"
    }
  }
}
```

### Load balancing

Pour plusieurs instances:

```yaml
services:
  lama1:
    image: lama-server
    ports:
      - "5001:5000"
  
  lama2:
    image: lama-server
    ports:
      - "5002:5000"

  nginx:
    depends_on:
      - lama1
      - lama2
    # Configuration upstream:
    # upstream lama_backend {
    #   server lama1:5000;
    #   server lama2:5000;
    # }
```

## Monitoring

### Health checks

```bash
# Accès direct
docker-compose exec lama-server curl http://localhost:5000/health

# Depuis l'hôte
curl http://localhost/health

# Avec jq pour prettifier
curl -s http://localhost/health | jq .
```

### Logs structurés

Configurer en `appsettings.json`:

```json
{
  "Logging": {
    "Console": {
      "IncludeScopes": true,
      "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff zzz"
    }
  }
}
```

### Métriques (optionnel)

Ajouter Prometheus:

```yaml
services:
  prometheus:
    image: prom/prometheus
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
    ports:
      - "9090:9090"
```

## Troubleshooting Docker

### Le conteneur s'arrête immédiatement

```bash
# Voir les logs
docker logs <container_id>

# Ou via compose
docker-compose logs lama-server

# Vérifier la configuration
docker run --rm lama-server dotnet --help
```

### Port déjà utilisé

```bash
# Trouver le port
sudo netstat -tulpn | grep :80
sudo netstat -tulpn | grep :443

# Ou avec lsof
lsof -i :80

# Tuer le processus
sudo kill -9 <PID>
```

### Problème de réseau entre conteneurs

```bash
# Vérifier la connectivité
docker-compose exec nginx ping lama-server

# Vérifier les DNS
docker-compose exec nginx nslookup lama-server

# Vérifier les règles firewall du conteneur
docker-compose exec nginx iptables -L
```

### Permissions de fichiers

```bash
# Si les logs n'écrivent pas
docker-compose exec lama-server chmod 777 /app/logs

# Vérifier le propriétaire
docker-compose exec lama-server ls -la /app
```

## Déploiement en production

### Secrets et configuration

Ne pas utiliser de fichiers .env en production:

```bash
# Utiliser les secrets Docker
docker secret create myapp_db_password ./db_password.txt

# Ou les secrets Compose v2.3+
compose:
  services:
    lama-server:
      secrets:
        - db_password
```

### Persistance des logs

```yaml
volumes:
  logs:
    driver: local
    
services:
  lama-server:
    volumes:
      - logs:/app/logs
```

### Sauvegarde et restauration

```bash
# Sauvegarder les volumes
docker run --rm -v logs:/volume \
  -v $(pwd):/backup \
  alpine tar czf /backup/logs-backup.tar.gz -C /volume .

# Restaurer
docker run --rm -v logs:/volume \
  -v $(pwd):/backup \
  alpine tar xzf /backup/logs-backup.tar.gz -C /volume
```

## Nettoyage

```bash
# Supprimer tout (attention!)
docker-compose down -v

# Nettoyer les images non utilisées
docker image prune -a --force --filter "until=240h"

# Nettoyer les conteneurs arrêtés
docker container prune -f

# Espace disque
docker system df
docker system prune -a --volumes
```

## Resources supplémentaires

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Nginx Docker Hub](https://hub.docker.com/_/nginx)
- [.NET Docker Hub](https://hub.docker.com/_/microsoft-dotnet)
- [Let's Encrypt with Docker](https://certbot.eff.org/docs/install.html#running-with-docker)

---

**Dernier mise à jour**: 2026-06-18

