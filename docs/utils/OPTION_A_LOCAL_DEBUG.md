# Option A : Debug Natif + PostgreSQL Docker

## Vue d'ensemble

**Option A** est la configuration idéale pour le développement local rapide :
- **PostgreSQL en Docker** (container isolé, données persistantes)
- **Lama.Server et Lama.WebApp en natif** (.NET 10 local, hot-reload via Rider/VS Code)
- **Communication localhost** entre services

## Topologie des Ports

```
┌─────────────────────────────────────────────────┐
│         Votre Machine (localhost)               │
├─────────────────────────────────────────────────┤
│                                                 │
│  Port 5200  ◄─── PostgreSQL (Docker)            │
│  Port 5201  ◄─── Lama.Server (Native .NET)      │
│  Port 5202  ◄─── Lama.WebApp (Native .NET)      │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Configuration

### 1. Variables d'environnement et appsettings

Les fichiers suivants ont été mis à jour pour utiliser la séquence 5200/5201/5202 :

#### `src/Server/Lama.Server/Properties/launchSettings.json`
- **Port d'écoute**: `5201`
- **Env**: `Development`

```json
"applicationUrl": "http://127.0.0.1:5201"
```

#### `src/Server/Lama.Server/appsettings.Development.json`
- **Connexion PostgreSQL**: `localhost:5200`

```json
"ConnectionStrings": {
  "LamaServerDb": "Host=localhost;Port=5200;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me;Ssl Mode=Disable;..."
}
```

#### `src/Server/Lama.Server/Data/LamaDbContextFactory.cs`
- **Connection string de design-time**: `localhost:5200`

#### `src/Web/Lama.GameWebApp/Properties/launchSettings.json`
- **Port d'écoute**: `5202`
- **URL du serveur**: `http://127.0.0.1:5201`

```json
"applicationUrl": "http://127.0.0.1:5202",
"environmentVariables": {
  "LAMA_SERVER_URL": "http://127.0.0.1:5201"
}
```

### 2. Docker Compose Configuration

Fichier: `docker-compose.local-debug-option-a.yml`

```yaml
services:
  postgres-lama:
    image: postgres:16-alpine
    ports:
      - "5200:5432"
    environment:
      POSTGRES_USER: lama_dev
      POSTGRES_PASSWORD: dev_password_change_me
      POSTGRES_DB: lama_dev
```

## Démarrage

### Méthode 1 : Script automatisé (recommandé)

```bash
./tools/scripts/start-local-debug-option-a.sh
```

Ce script va :
1. ✓ Démarrer PostgreSQL sur 5200
2. ✓ Afficher la topologie
3. ✓ Donner les instructions de démarrage

### Méthode 2 : Manuel

**Terminal 1 - PostgreSQL**:
```bash
cd /home/philippe/RiderProjects/Games/Lama
docker-compose -f docker-compose.local-debug-option-a.yml up
```

**Terminal 2 - Lama.Server (port 5201)**:
```bash
cd /home/philippe/RiderProjects/Games/Lama
dotnet run --project src/Server/Lama.Server
```

**Terminal 3 - Lama.WebApp (port 5202)**:
```bash
cd /home/philippe/RiderProjects/Games/Lama
dotnet run --project src/Web/Lama.GameWebApp
```

## Accès à l'application

- **WebApp**: [http://localhost:5202](http://localhost:5202)
- **API Server (debug)**: [http://localhost:5201](http://localhost:5201)
- **PostgreSQL**: `localhost:5200` (via `psql` ou clients DB)

### Vérifier la santé de PostgreSQL

```bash
docker-compose -f docker-compose.local-debug-option-a.yml ps
```

Expected output:
```
postgres-lama-option-a   Up (healthy)   0.0.0.0:5200->5432/tcp
```

## Utilisation

### Créer un compte et se connecter

1. Aller sur [http://localhost:5202](http://localhost:5202)
2. Clic sur **"S'inscrire"**
3. Remplir le formulaire (pseudo, motrin, email optionnel)
4. Connexion automatique après création
5. Accès à **"Mes parties"** et profil

### Arrêter les services

```bash
# PostgreSQL et Docker:
docker-compose -f docker-compose.local-debug-option-a.yml down

# Note: Ctrl+C dans les terminaux .NET arrête Server et WebApp
```

### Nettoyer les données (réinitialiser la DB)

```bash
docker-compose -f docker-compose.local-debug-option-a.yml down -v
# Le flag -v supprime les volumes Docker (données PostgreSQL)
```

## Points clés

✅ **Avantages d'Option A**:
- Débogage natif Rider/VS Code (breakpoints, step-into)
- Hot-reload rapide lors des modifications locales
- Données persistantes dans PostgreSQL
- Pas de rebuild Docker (très rapide)
- Idéal pour le développement et l'itération

⚠️ **À savoir**:
- PostgreSQL doit être arrêté avant le redémarrage (port 5200 occupé)
- Auto-migration active dans `appsettings.Development.json`
- Variables d'env (`LAMA_SERVER_URL`, etc.) lues au démarrage du service

## Dépannage

### Erreur: "port 5200 already in use"
```bash
# Arrêter les services:
docker-compose -f docker-compose.local-debug-option-a.yml down

# Ou tuer le processus PostgreSQL:
lsof -ti:5200 | xargs kill -9
```

### Erreur: "connection refused on localhost:5200"
→ PostgreSQL n'a pas démarré ou est encore en cours de démarrage. Attendre 5-10 secondes.

### Erreur IIS Express ou autre port déjà utilisé
Si `5201` ou `5202` sont bloqués, modifier dans `launchSettings.json` et relancer.

## Évolutivité

Cette topologie supporte :
- ✓ Multiple joueurs simultanés (via signalR server)
- ✓ Jeux persistants en base
- ✓ Sessions multiples
- ✓ Montée en charge (scaling horizontal du server via load balancer)

Pour passer à **Option B** (pré-production avec tous les services dockerisés), voir `DOCKER_DEPLOYMENT.md`.

