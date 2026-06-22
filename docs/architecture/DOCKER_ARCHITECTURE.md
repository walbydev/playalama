# Architecture Docker - Lama Projet

**Dernière mise à jour**: 2026-06-19  
**Responable**: Restructuration complète - Nettoyage infra  
**Version cible**: Lama WebApp 1.1.0

---

## 📋 Vue d'ensemble

Le projet **Lama** utilise Docker pour :
- **Local** (`docker-compose.local.yml`) : Développement avec hot-reload
- **Production** (`docker-compose.prod.yml`) : VPS déploiement sécurisé

### Structure actuelle
```
tools/docker/
├── Dockerfile.server              # Build API serveur ASP.NET 10
├── Dockerfile.webapp              # Build WebApp Blazor ASP.NET 10
├── docker-compose.local.yml      # Dev: ports 80/443, debug actif
├── docker-compose.prod.yml       # Prod: volume-mounted sur VPS
├── nginx-playalama.conf          # Config reverse proxy nginx
└── DOCKER_ARCHITECTURE.md        # Ce fichier

.deploy/                          # ⚠️ Secrets & certs (./gitignore)
├── certs/                        # Certificats Let's Encrypt (prod)
├── certbot-webroot/              # Validation ACME
└── .gitignore                    # Exclut secrets

Dockerfile (ROOT) [DÉPRÉCIRÉ]      # ⚠️ À supprimer après migration
docker-compose.yml (ROOT) [DÉPRÉCIRÉ] # ⚠️ À supprimer après migration
```

---

## 🏃 Commandes rapides

### Développement local
```bash
# Démarrer services locaux (hot-reload)
docker compose -f tools/docker/docker-compose.local.yml up

# Arrêter
docker compose -f tools/docker/docker-compose.local.yml down

# Rebuild sans cache
docker compose -f tools/docker/docker-compose.local.yml up --build --no-cache
```

### Production VPS
```bash
# SSH sur VPS, puis à /opt/playalama:
docker compose -f docker-compose.prod.yml up -d --build

# Consulter logs
docker compose -f docker-compose.prod.yml logs lama-server

# Arrêter proprement
docker compose -f docker-compose.prod.yml down
```

---

## 📦 Dockerfile.server - Étapes

| Stage | Rôle | Image | Taille |
|-------|------|-------|--------|
| `builder` | Compilation C# -> Release | mcr.microsoft.com/dotnet/sdk:10.0 | ~2GB |
| `runtime` | Exécution seulement | mcr.microsoft.com/dotnet/aspnet:10.0 | ~200MB |

**Optimisations** :
- Multi-stage : supprime SDK du runtime (-1.8 GB)
- Assets langues copiés en build : `/app/assets/languages`
- Health check intégré (Docker/Compose utilise cet endpoint)

---

## 🐋 docker-compose.local.yml

### Fichiers importants

| Service | Port | Volume | Note |
|---------|------|--------|------|
| **lama-server** | 5000 | ./assets/languages | Debug enabled |
| **nginx** | 80, 443 | ./site/static | Certs autosignés en `.deploy/certs/` |

### Variables d'environnement

```yaml
# lama-server
ASPNETCORE_ENVIRONMENT: Development  # Logs verbeux
LAMA_SERVER_ALLOW_SHUTDOWN: "true"  # ✓ Permet /internal/shutdown
```

### Volumes
- `assets/languages` : RO (read-only)
- `logs/` : RW pour traces runtime

### Health checks
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
  interval: 10s  # Plus rapide en dev
  retries: 3
```

---

## 🌍 docker-compose.prod.yml

### Fichiers importants

| Service | Port | Map | Note |
|---------|------|-----|------|
| **lama-webapp** | 5050 | Interne au network | UI Blazor publique via nginx |
| **lama-server** | 5000 | Interne au network | API gameplay / auth |
| **nginx** | 80, 443 | 0.0.0.0:80/443 | Reverse proxy public |

### Variables d'environnement

```yaml
# lama-server
ASPNETCORE_ENVIRONMENT: Production   # Logs min, perf max
LAMA_SERVER_ALLOW_SHUTDOWN: "false" # ❌ Pas d'arrêt externe
```

### Volumes
- `tools/docker/nginx-playalama.conf` : Config reverse proxy
- `./.deploy/certs/` : Certificats Let's Encrypt (prod)
- `artifacts/zip/` : Fichiers binaires téléchargement

### Health checks
```yaml
healthcheck:
  interval: 30s  # Production: moins fréquent
```

---

## 🔐 Sécurité

### .deploy/ - Secrets (JAMAIS versionnés)

```bash
# Fichiers à exclure (.gitignore)
.deploy/certs/live/playalama.online/
  ├── fullchain.pem     # Certificat SSL
  ├── privkey.pem       # Clé privée
  └── chain.pem         # Chaîne

.deploy/certbot-webroot/   # Validation ACME
```

### nginx-playalama.conf - Header de sécurité

```nginx
add_header Strict-Transport-Security "max-age=63072000..."
add_header X-Frame-Options "SAMEORIGIN"
add_header X-Content-Type-Options "nosniff"
add_header X-XSS-Protection "1; mode=block"
```

---

## 🚀 Déploiement production

### Flux complet

```bash
# 1. Sur LOCAL: préparer
dotnet build
dotnet test

# 2. Exécuter script déploiement
./tools/scripts/deploy-static-site.sh \
  --mode prod \
  --target debian@playalama.online \
  --ssh-key ~/.ssh/machines/playalama.key

# 3. Script va:
#    - Copier tools/docker/Dockerfile.server → /opt/playalama/Dockerfile
#    - Copier docker-compose.prod.yml → /opt/playalama/docker-compose.yml
#    - Copier nginx-playalama.conf → /opt/playalama/tools/docker/nginx-playalama.conf
#    - Copier site/static/ (accueil + download + assets) → /opt/playalama/site/static/
#    - Exécuter: docker compose up -d --build

# 4. Valider
curl https://playalama.online/health
```

### Fichiers critiques à copier

| Local | Distant |
|-------|---------|
| `tools/docker/Dockerfile.server` | `/opt/playalama/Dockerfile` |
| `tools/docker/docker-compose.prod.yml` | `/opt/playalama/docker-compose.yml` |
| `tools/docker/nginx-playalama.conf` | `/opt/playalama/tools/docker/nginx-playalama.conf` |
| `site/static/` | `/opt/playalama/site/static/` |
| `assets/languages/` | `/opt/playalama/assets/languages/` |
| `artifacts/zip/` | `/opt/playalama/artifacts/zip/` |

---

## ⚠️ Problèmes courants

### 1. Dockerfile introuvable au déploiement

**Symptôme** : `failed to read dockerfile: open Dockerfile: no such file or directory`

**Cause** : Script de déploiement n'a pas copié le Dockerfile

**Solution** :
```bash
# Vérifier que tools/docker/Dockerfile.server existe
ls -la tools/docker/Dockerfile.server

# Script de déploiement copie vers:
Dockerfile → /opt/playalama/Dockerfile (renommé)

# Sur VPS, vérifier:
ssh debian@playalama.online "ls /opt/playalama/Dockerfile"
```

### 2. Assets langues manquantes

**Symptôme** : Erreur `FileNotFoundException: 'assets/languages/fr'`

**Cause** : Volume d'assets pas mounté en Docker

**Check** :
```bash
docker compose -f docker-compose.prod.yml exec lama-server ls -la /app/assets/languages/

# Ou depuis Dockerfile:
ls -la assets/languages/fr/
```

### 3. nginx ne trouve pas l'upstream

**Symptôme** : `502 Bad Gateway`

**Cause** : lama-server pas sur le même network Docker ou mal configuré

**Check** :
```bash
docker compose -f docker-compose.prod.yml exec nginx cat /etc/hosts

# Doit voir: 172.x.x.x lama-server
```

---

## 📝 Migration depuis structure OLD

### État OLD (dépréciée)
```
Dockerfile                 # À la racine
docker-compose.yml         # À la racine
certs/                     # À la racine (pollue repo)
certbot-webroot/          # À la racine (pollue repo)
```

### État NEW (actuelle)
```
tools/docker/
├── Dockerfile.server      # Renommé de ~/Dockerfile
├── docker-compose.local.yml
├── docker-compose.prod.yml
└── nginx-playalama.conf

.deploy/                  # .gitignore: secrets exlus
├── certs/
└── certbot-webroot/
```

### Commandes de cleanup

```bash
# 1. Archiver anciennes versions
cp Dockerfile Dockerfile.old.$(date +%s)
cp docker-compose.yml docker-compose.old.$(date +%s)

# 2. Supprimer originals (garder backup!)
# rm Dockerfile docker-compose.yml

# 3. Vérifier que références sont mises à jour
grep -r "docker-compose.yml" . --exclude-dir=.git

# 4. Tester
docker compose -f tools/docker/docker-compose.local.yml config
```

---

## 📚 Références documentations liées

| Fichier | Contenu |
|---------|---------|
| `AGENTS.md` | Conventions projet + workflows |
| `deploy-static-site.sh` | Script déploiement automatisé |
| `src/Server/Lama.Server/README.md` | API endpoints serveur |
| `docs/utils/HTTPS_QUICK_START.md` | Setup SSL Certbot |

---

## ✅ Checklist déploiement

- [ ] `tools/docker/Dockerfile.server` existe
- [ ] `tools/docker/docker-compose.prod.yml` personnalisé pour prod
- [ ] `.deploy/certs/` et `.deploy/certbot-webroot/` dans `.gitignore`
- [ ] VPS: `/opt/playalama/Dockerfile` existant après deploy
- [ ] VPS: `docker compose -f docker-compose.prod.yml config` válide
- [ ] VPS: `docker compose -f docker-compose.prod.yml up -d`
- [ ] Test: `curl https://playalama.online/health` → 200 OK

---

## 🔄 Cycle de maintenance (2 ans +)

Pour comprendre la structure en 2026+ :

1. **Pas de changements** → Docker reste modular sous `tools/docker/`
2. **Nouveau service** → Ajouter dans `docker-compose.*.yml`
3. **Changement build** → Modifier `Dockerfile.server` seulement
4. **Sécurité certs** → `.deploy/` jamais versionné
5. **Rollback** : `git checkout -- tools/docker/` ramène version précédente

---

**Mainteneur actuel** : Philippe (2026)  
**Dernière révision** : 2026-06-19  
**Status** : ✅ Production

