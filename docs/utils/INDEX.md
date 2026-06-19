# docs/utils — Documentation Utilitaire & Quick Start

Index des guides pratiques pour développement et exploitation.

## Quick Start (Recommandé pour démarrer)

- **[OPTION_A_QUICKSTART.md](./OPTION_A_QUICKSTART.md)** ⭐
  - 3 ports (5200/5201/5202)
  - PostgreSQL en Docker + Services natifs
  - Démarrage < 2 minutes
  - **Recommandé pour développement local**

## Documentation Détaillée

- **[OPTION_A_LOCAL_DEBUG.md](./OPTION_A_LOCAL_DEBUG.md)**
  - Topologie complète Option A
  - Configuration des ports et variables d'env
  - Dépannage et scripts d'exploitation
  - Points clés et évolutivité

## Autres Resources

- `../../AGENTS.md` — Memo IA principal (architecture, conventions, fichiers prioritaires)
- `../../README.md` — Vue d'ensemble projet
- `../architecture/DOCKER_DEPLOYMENT.md` — Déploiement Docker multi-conteneurs
- `../roadmap/` — Jalons, progress, plans
- `../evolutions/` — Propositions futures

## Commandes Rapides

### Démarrage Option A
```bash
make option-a-start            # PostgreSQL sur 5200
make option-a-server           # Server sur 5201 (Terminal 1)
make option-a-webapp           # WebApp sur 5202 (Terminal 2)
```

### Santé & Logs
```bash
make health-option-a           # Vérifier services
make option-a-logs             # Logs PostgreSQL
```

### Cleanup
```bash
make option-a-stop             # Arrêter PostgreSQL
make option-a-clean            # Réinitialiser DB
```

### Tests
```bash
./tools/scripts/test-option-a.sh  # Test complet Option A
```

## Docker Compose Files

- `docker-compose.local-debug-option-a.yml` — Option A (PostgreSQL uniquement)
- `tools/docker/docker-compose.local.yml` — Stack locale (legacy)
- `tools/docker/docker-compose.prod.yml` — Production

## Configuration des Services

### Lama.Server
- **Port**: 5201 (défini dans `src/Server/Lama.Server/Properties/launchSettings.json`)
- **DB**: localhost:5200 (défini dans `appsettings.Development.json`)
- **Env**: Development (auto-migrate activé)

### Lama.WebApp
- **Port**: 5202 (défini dans `src/Web/Lama.WebApp/Properties/launchSettings.json`)
- **Server URL**: http://127.0.0.1:5201 (variable `LAMA_SERVER_URL`)
- **Env**: Development

### PostgreSQL (Docker)
- **Port externe**: 5200 → 5432 (interne)
- **User**: lama_dev
- **Password**: dev_password_change_me
- **Database**: lama_dev

## Schéma DB

Tables principales :
- `players` — Comptes utilisateurs
  - `player_id` (UUID)
  - `username` (text, unique)
  - `email` (text, nullable, unique)
  - `password_hash` (text, nullable, PBKDF2-SHA512)
  - `created_at` (timestamp)
- `games` — Parties
- `game_states` — États de jeu persistants
  - (et autres tables d'infrastructure)

## Envars Principales

```bash
# Option A (défauts)
ASPNETCORE_ENVIRONMENT=Development
LAMA_SERVER_URL=http://127.0.0.1:5201
Database:AutoMigrate=true          # Applique migrations au démarrage

# PostgreSQL (défauts, à adapter si nécessaire)
POSTGRES_USER=lama_dev
POSTGRES_PASSWORD=dev_password_change_me
POSTGRES_DB=lama_dev
```

## Support

Pour des questions ou rapporter des bugs :
1. Vérifier les logs : `make option-a-logs`
2. Consulter `OPTION_A_DETAILED.md` pour dépannage
3. Voir `../../docs/architecture/` pour context technique

