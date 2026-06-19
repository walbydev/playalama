# PostgreSQL Start Guide - LAMA Server Dev

Démarrage rapide de PostgreSQL en local avec Docker pour le développement de LAMA Server.

## Prérequis

- Docker installé et en fonctionnement
- `docker-compose` (inclus avec Docker Desktop)
- `.NET 10 SDK` pour Lama.Server

## Démarrage rapide en 3 étapes

### 1. Lancer PostgreSQL + PgAdmin

```bash
# Depuis la racine du projet
docker compose -f docker-compose.postgresdev.yml up -d

# Vérifier que les containers sont en place
docker compose -f docker-compose.postgresdev.yml ps

# Logs pour debug
docker compose -f docker-compose.postgresdev.yml logs -f postgres-lama
```

**Outputs attendus** :

```
NAME                    IMAGE                   STATUS
postgres-lama-dev       postgres:16-alpine      Up 2 minutes (healthy)
pgadmin-lama-dev        dpage/pgadmin4:latest   Up 2 minutes
```

### 2. Attendre que PostgreSQL soit prêt

```bash
# Vérifier la santé du container (healthcheck)
docker compose -f docker-compose.postgresdev.yml ps

# Ou connectez-vous directement
psql -h localhost -p 55432 -U lama_dev -d lama_dev \
  -c "SELECT version();"
```

### 3. Vérifier l'initialisation des schémas

```bash
# Les scripts sous tools/postgres sont montes automatiquement
# et executes au premier boot (volume vierge).

# Verifier que les 3 schemas existent
docker exec -it postgres-lama-dev psql -U lama_dev -d lama_dev -c "\dn"

# Verifier quelques tables
docker exec -it postgres-lama-dev psql -U lama_dev -d lama_dev -c "\dt sessions.*"
docker exec -it postgres-lama-dev psql -U lama_dev -d lama_dev -c "\dt history.*"
docker exec -it postgres-lama-dev psql -U lama_dev -d lama_dev -c "\dt rating.*"
```

## Accès aux interfaces

### PostgreSQL CLI

```bash
# Connexion directe au container
docker exec -it postgres-lama-dev psql -U lama_dev -d lama_dev

# Ou via psql local
psql -h localhost -p 55432 -U lama_dev -d lama_dev
```

**Commandes SQL utiles** :

```sql
-- Lister les schémas
\dn

-- Lister les tables d'un schéma
\dt sessions.

-- Vérifier les permissions
SELECT * FROM information_schema.role_table_grants WHERE table_schema = 'sessions';

-- Voir l'état d'une vue
\dv sessions.active_games_summary

-- Audit : dernières 10 parties
SELECT * FROM history.completed_games ORDER BY ended_at DESC LIMIT 10;

-- Classement (Open)
SELECT * FROM rating.top_players_open;
```

### PgAdmin Web UI

- **URL** : `http://localhost:5050`
- **Email** : `admin@lama.local` (par défaut)
- **Password** : `admin` (par défaut, à changer)

**Première connexion** :

1. Allez sur `http://localhost:5050`
2. Login avec `admin@lama.local` / `admin`
3. **Add New Server** :
   - Name: `LamaDB-Dev`
   - Connection tab:
     - Host: `postgres-lama` (nom du container)
     - Port: `5432`
     - Maintenance DB: `lama_dev`
     - Username: `lama_dev`
     - Password: `dev_password_change_me`
   - Save

Vous pouvez alors explorer graphiquement les schémas, tables et exécuter des queries.

## Configuration Lama.Server

## Choisir une seule strategie d'initialisation

Sur un volume vierge, **n'utilisez pas simultanement**:

1. les scripts Docker `docker-entrypoint-initdb.d` (actifs dans `docker-compose.postgresdev.yml`), et
2. `dotnet-ef database update` avec une migration qui cree les memes tables.

Sinon vous aurez des erreurs du type `relation "..." already exists`.

Strategie recommandee actuellement:

- **Dev rapide**: scripts SQL auto-executes au premier boot (pas de `database update` juste apres)
- **Dev full EF**: desactiver temporairement les mounts de scripts puis utiliser `dotnet-ef`

Le fichier `appsettings.Development.json` est automatiquement configuré pour se connecter à PostgreSQL locale.

```json
"ConnectionStrings": {
  "LamaServerDb": "Host=localhost;Port=55432;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me;..."
}
```

**Note** : le serveur .NET est lance sur l'hote avec `dotnet run`, donc `Host=localhost` est le choix attendu.

### Lancer Lama.Server localement

```bash
# Terminal 1 : PostgreSQL
docker compose -f docker-compose.postgresdev.yml up

# Terminal 2 : Lama.Server (ou terminal séparé)
dotnet run --project src/Server/Lama.Server --configuration Development

# Le serveur écoute sur http://localhost:8080
curl http://localhost:8080/health
```

## Cleanup

### Arrêter les containers

```bash
docker compose -f docker-compose.postgresdev.yml down
```

### Supprimer les données persistantes

```bash
# ATTENTION : Cette action supprime TOUTES les données !
docker compose -f docker-compose.postgresdev.yml down -v
```

### Redémarrer avec données vierges

```bash
docker compose -f docker-compose.postgresdev.yml down -v
docker compose -f docker-compose.postgresdev.yml up -d

# Important: les scripts /docker-entrypoint-initdb.d sont rejoues seulement apres down -v
```

## Variables d'environnement

Vous pouvez personnaliser la configuration via variables `.env` :

```bash
# .env (à créer à la racine du projet)
POSTGRES_PORT=55432
POSTGRES_PASSWORD=ma_custom_password
PGADMIN_EMAIL=monmail@example.com
PGADMIN_PASSWORD=monmotdepasse
```

Puis relancer :

```bash
docker compose -f docker-compose.postgresdev.yml down -v
docker compose -f docker-compose.postgresdev.yml up -d
```

## Troubleshooting

### PostgreSQL ne démarre pas

```bash
# Vérifiez les logs
docker compose -f docker-compose.postgresdev.yml logs postgres-lama

# Réinitialisez
docker compose -f docker-compose.postgresdev.yml down -v
docker compose -f docker-compose.postgresdev.yml up -d
```

### Cannot connect to psql

```bash
# Vérifiez le container est sain
docker container inspect postgres-lama-dev | grep -A 5 "Status"

# Attendez quelques secondes, le container peut être en démarrage
# Vérifiez aussi que le port 5432 n'est pas déjà utilisé
lsof -i :5432
```

### Permission denied sur les scripts SQL

```bash
# Relancer avec les bonnes permissions
docker exec -u root postgres-lama-dev chmod +r /docker-entrypoint-initdb.d/*
docker compose -f docker-compose.postgresdev.yml restart postgres-lama
```

## Monitoring

### Via docker stats

```bash
docker stats postgres-lama-dev pgadmin-lama-dev
```

### Via PostgreSQL

```sql
-- Connexions actives
SELECT datname, count(*) FROM pg_stat_activity GROUP BY datname;

-- Taille des base de données
SELECT datname, pg_size_pretty(pg_database_size(datname)) FROM pg_database;

-- Taille des tables du schéma sessions
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname || '.' || tablename)) as size
FROM pg_tables
WHERE schemaname = 'sessions'
ORDER BY pg_total_relation_size(schemaname || '.' || tablename) DESC;
```

## Production

Pour une déploiement production, voir :
- `docs/POSTGRESQL_ARCHITECTURE.md` → section "Production"
- `docs/DOCKER_DEPLOYMENT.md`
- `docs/HTTPS_DEPLOYMENT.md`

Vous utiliserez la configuration `appsettings.Production.json` qui se connecte via variables d'environnement à une instance PostgreSQL standalone existante.

## Références

- [PostgreSQL Docker Hub](https://hub.docker.com/_/postgres)
- [PgAdmin Docker Hub](https://hub.docker.com/r/dpage/pgadmin4)
- [PostgreSQL Docs](https://www.postgresql.org/docs/)
- [LAMA PostgreSQL Architecture](./POSTGRESQL_ARCHITECTURE.md)

