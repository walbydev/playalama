🚀 # COMMENCEZ ICI

## ❌ Votre problème

```
$ curl -I https://playalama.online
curl: (7) Failed to connect to playalama.online port 443 after 6 ms: Could not connect to server
```

## ✅ Votre solution

**Temps total : 2-3 heures**
**Effort : 1-3 commandes simples**

---

## 📖 Lire en premier (15 min)

Choisissez l'une de ces autres documentations :

```bash
# Pour vue d'ensemble rapide
cat SOLUTION_SUMMARY.md

# Pour checklist d'action
cat HTTPS_QUICK_START.md

# Pour livrable complet
cat DELIVERABLE_SUMMARY.md
```

---

## 🎯 Trois options de déploiement

### ⭐ OPTION 1 : Script automatisé (RECOMMANDÉ)

**Timeline** : 2 heures
**Commandes** : 2 seulement
**Complexité** : ⭐ Basique

```bash
# Rendre exécutable
chmod +x tools/scripts/setup-https.sh

# Exécuter (le reste se fait tout seul)
sudo tools/scripts/setup-https.sh

# Vérifier après
bash tools/scripts/verify-https.sh playalama.online
```

### 🐳 OPTION 2 : Docker Compose

**Timeline** : 1-2 heures
**Commandes** : 3-4
**Complexité** : ⭐⭐ Intermédiaire

```bash
docker-compose build
docker-compose up -d
# Certificats Let's Encrypt (sur hôte)
sudo certbot certonly --standalone -d playalama.online
```

Voir `docs/DOCKER_DEPLOYMENT.md`

### 📖 OPTION 3 : Manuel pas à pas

**Timeline** : 3 heures
**Commandes** : ~10
**Complexité** : ⭐⭐⭐ Avancé

Voir `docs/HTTPS_DEPLOYMENT.md` → "Installation manuelle"

---

## ✔️ Avant de commencer

```bash
# 1. Vérifier DNS
nslookup playalama.online
# Devrait retourner l'IP du serveur

# 2. Vérifier ports ouverts
# Port 80 doit être accessible
curl -I http://playalama.online
# Devrait retourner 200 OK

# 3. Vérifier .NET 10 ou Docker
dotnet --version     # Pour Option 1/3
docker --version     # Pour Option 2

# 4. Vérifier accès root
sudo whoami
# Devrait af "root"
```

---

## 🎬 Action immédiate (< 1 min)

### Je choisis Option 1 (automatique)

```bash
cd /home/philippe/RiderProjects/Games/Lama

# Rendre executable
chmod +x tools/scripts/setup-https.sh

# Lancer installation (le script fait tout)
# ⚠️ Cela prendra ~1-2 heures
sudo tools/scripts/setup-https.sh

# Une fois terminé, vérifier
bash tools/scripts/verify-https.sh playalama.online

# Tester
curl -I https://playalama.online
# Devrait retourner 200 OK avec certificat SSL
```

### Je choisis Option 2 (Docker)

```bash
cd /home/philippe/RiderProjects/Games/Lama

# Build
docker-compose build

# Start
docker-compose up -d

# Vérifier
bash tools/scripts/verify-https.sh playalama.online
```

### Je choisis Option 3 (manuel)

```bash
# Lire le guide
less docs/HTTPS_DEPLOYMENT.md

# Suivre les étapes 1-6
```

---

## 📞 Si ça ne marche pas

```bash
# Diagnostic complet
bash tools/scripts/verify-https.sh playalama.online

# Voir les erreurs nginx
sudo tail -f /var/log/nginx/playalama-error.log

# Vérifier la config nginx
sudo nginx -t

# Vérifier certificats
sudo certbot certificates
```

---

## ✅ Quand c'est fait

```bash
# Tester HTTP (devrait rediriger)
curl -I http://playalama.online
→ 301 Moved Permanently

# Tester HTTPS
curl -I https://playalama.online
→ 200 OK

# Tester API
curl -s https://playalama.online/health | jq .
→ {"status":"ok", "utcNow":"..."}
```

---

## 📚 Documentation complète

| Document | Durée | Niveau |
|----------|-------|--------|
| **SOLUTION_SUMMARY.md** | 15 min | ⭐ |
| **HTTPS_QUICK_START.md** | 15 min | ⭐ |
| **docs/HTTPS_DEPLOYMENT.md** | 30 min | ⭐⭐ |
| **docs/HTTPS_MIGRATION_PLAN.md** | 20 min | ⭐⭐ |
| **docs/DOCKER_DEPLOYMENT.md** | 40 min | ⭐⭐⭐ |
| **FILES_INDEX.md** | 10 min | ⭐ |

---

## 🎉 C'est tout!

**Vous avez une solution complète prête à déployer.**

Choisissez votre option et lancez! ✨

---

**Besoin d'aide?** : Consultez la documentation appropriée ci-dessus.

**Prêt?** : `sudo tools/scripts/setup-https.sh` (Option 1)

