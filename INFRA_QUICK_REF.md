# 🚀 QUICK REFERENCE - Lama Infrastructure 2026-06-19

**Tl;dr**: Structure Docker + déploiement robuste

---

## 📁 Structure nouvelle

```
tools/docker/
├── Dockerfile.server              # Build ASP.NET 10 production
├── docker-compose.local.yml      # Dev local
├── docker-compose.prod.yml       # Prod VPS
├── nginx-playalama.conf          # Reverse proxy
└── DOCKER_ARCHITECTURE.md        # Doc complète 350L

site/static/
├── index.html                   # Accueil public https://playalama.online/
├── download/index.html          # Téléchargements
└── assets/                      # CSS/JS/images partagés
```

---

## ⚡ Commandes essentielles

### 🔧 Local (développement)

```bash
# Build + run dev
docker compose -f tools/docker/docker-compose.local.yml up --build

# Stop
docker compose -f tools/docker/docker-compose.local.yml down

# Logs serveur
docker compose -f tools/docker/docker-compose.local.yml logs -f lama-server

# Bash dans container
docker compose -f tools/docker/docker-compose.local.yml exec lama-server bash
```

### 🌍 Production (VPS)

```bash
# Test sec (DRY RUN)
./tools/scripts/deploy-static-site.sh \
  --mode prod \
  --target debian@playalama.online \
  --ssh-key ~/.ssh/machines/playalama.key \
  --dry-run

# Déployer réel
./tools/scripts/deploy-static-site.sh \
  --mode prod \
  --target debian@playalama.online \
  --ssh-key ~/.ssh/machines/playalama.key

# Vérifier sur VPS (post-deploy)
ssh debian@playalama.online
cd /opt/playalama
docker compose ps
docker compose logs lama-server | tail -30
```

---

## 🔐 Sécurité

```bash
# Secrets JAMAIS versionés
.deploy/certs/
.deploy/certbot-webroot/

# Vérifier .gitignore OK
grep ".deploy" .gitignore
grep "*.pem" .gitignore
grep "*.key" .gitignore
```

---

## 🧪 Tests

```bash
# Syntaxe bash
bash -n tools/scripts/deploy-static-site.sh

# Compose syntaxe
docker compose -f tools/docker/docker-compose.local.yml config --quiet
docker compose -f tools/docker/docker-compose.prod.yml config --quiet

# Dockerfile build
docker build -f tools/docker/Dockerfile.server .
```

---

## 📚 Docs

| Doc | Ligne | Contenu |
|-----|------|---------|
| `tools/docker/DOCKER_ARCHITECTURE.md` | 350 | Tout Docker |
| `RESTRUCTURATION_INFRA_2026-06-19.md` | 280 | Guide migration |
| `RESTRUCTURATION_RÉSUMÉ.md` | 220 | Summary exécutif |

---

## 🛠️ Troubleshooting rapide

**Volume manquant** → Check `tools/docker/docker-compose.*.yml`  
**502 Gateway** → `docker compose logs lama-server`  
**Dockerfile missing prod** → Script copie auto maintenant ✅  
**VPS déjà désordonné** → `./tools/scripts/organize-vps-playalama.sh --base-dir /opt/playalama --apply`
**Secrets leakés** → Check `.gitignore` + `.deploy/.gitignore`

---

## 🔄 Maintenance future

### Ajouter service
```yaml
# tools/docker/docker-compose.local.yml OU prod.yml
services:
  new-service:
    image: xyz:latest
    networks:
      - lama-network
```

### Changer build
```dockerfile
# tools/docker/Dockerfile.server
# Puis: docker compose -f tools/docker/docker-compose.local.yml up --build
```

### Rollback
```bash
git checkout -- tools/docker/
```

---

**Voir `tools/docker/DOCKER_ARCHITECTURE.md` pour détails complets** 📖

