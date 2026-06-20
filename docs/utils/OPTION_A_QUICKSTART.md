# Lama v1.1.0 — Option A Quick Start

## Topologie (3 ports)

```
5200  ← PostgreSQL (Docker)
5201  ← Lama.Server (.NET natif local)
5202  ← Lama.GameWebApp (.NET natif local)
```

## Démarrage rapide ⚡

### 1️⃣ Lancer PostgreSQL (Docker)

```bash
make option-a-start
```

Ou manuellement :
```bash
docker-compose -f docker-compose.local-debug-option-a.yml up -d
```

### 2️⃣ Terminal 1 → Lama.Server sur 5201

```bash
make option-a-server
```

Ou :
```bash
dotnet run --project src/Server/Lama.Server
```

### 3️⃣ Terminal 2 → Lama.GameWebApp sur 5202

```bash
make option-a-webapp
```

Ou :
```bash
dotnet run --project src/Web/Lama.GameWebApp
```

### 4️⃣ Accès

- **Web App**: [http://localhost:5202](http://localhost:5202)
- **API Server**: [http://localhost:5201](http://localhost:5201)
- **DB**: `psql -h localhost -p 5200 -U lama_dev -d lama_dev`

## Commandes Utiles

```bash
# Vérifier l'état
make health-option-a

# Voir les logs PostgreSQL
make option-a-logs

# Arrêter PostgreSQL
make option-a-stop

# Réinitialiser DB (supprimer tout)
make option-a-clean

# Test complet
./tools/scripts/test-option-a.sh
```

## Points clés

✅ **Avantages**:
- Debug natif Rider/VS Code (breakpoints, hot-reload)
- Reconstruction rapide (pas de rebuild Docker)
- Données persistantes PostgreSQL
- Idéal pour développement itératif

⚠️ **À savoir**:
- Auto-migration active à la première connexion au Server
- Logs SQL activés en mode Development
- Ports hardcodés dans launchSettings.json (modifiable)

## Dépannage

**Port 5200 déjà occupé?**
```bash
lsof -ti:5200 | xargs kill -9
```

**Voir l'état PostgreSQL**:
```bash
docker-compose -f docker-compose.local-debug-option-a.yml ps
```

**Logs détaillés DB**:
```bash
docker-compose -f docker-compose.local-debug-option-a.yml logs postgres-lama -f
```

## Documentation complète

→ Voir `docs/utils/OPTION_A_LOCAL_DEBUG.md`

