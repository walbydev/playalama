# PostgreSQL Architecture pour LAMA Server

**Date** : 2026-06-18  
**État** : Architecture proposée (implémentation à venir)

## Vue d'ensemble

Stratégie de persistance PostgreSQL avec séparation stricte des domaines métier.

### Contexte

- **Actuellement** : Lama.Server stocke tout en mémoire (GAM Hub State)
- **Objectif** : Persistance PostgreSQL multi-environnement
- **Contrainte** : Garder le mode local CLI compatible
- **Environnements** : Dev (Docker) + Production (PostgreSQL existant)

---

## Architecture Schémas PostgreSQL

Trois schémas PostgreSQL distincts pour séparation des responsabilités :

### 1. `sessions` - Parties en cours (volatile, transactionnel)

État d'une partie non terminée. Très actif (lectures/écritures fréquentes).

**Tables** :

```
sessions.games
├── game_id UUID PRIMARY KEY
├── game_level ENUM (Standard, Competitive, Tournament, Casual)
├── board_size INT (15, 17, 21)
├── rack_size INT (7, 8, 9)
├── min_word_length INT
├── language VARCHAR (fr, en, etc.)
├── queue ENUM (open, tournament, global)
├── host_player_id UUID (FK sessions.players)
├── tournament_id UUID (nullable, FK history.tournaments)
├── status ENUM (created, active, paused, ended, abandoned)
├── created_at TIMESTAMP UTC
├── updated_at TIMESTAMP UTC
├── ended_at TIMESTAMP UTC (nullable)

sessions.players_in_game
├── player_session_id UUID PRIMARY KEY
├── game_id UUID (FK sessions.games)
├── player_id UUID (FK rating.players)
├── nickname VARCHAR
├── is_host BOOLEAN
├── player_index INT (ordre tour)
├── joined_at TIMESTAMP UTC

sessions.board_state
├── game_id UUID (FK sessions.games)
├── board_json JSONB (état complet plateau + tuiles)
├── updated_at TIMESTAMP UTC

sessions.rack_state
├── game_id UUID (FK sessions.games)
├── player_session_id UUID (FK sessions.players_in_game)
├── rack_json JSONB (tuiles du rack)
├── updated_at TIMESTAMP UTC

sessions.turn_log
├── turn_id UUID PRIMARY KEY
├── game_id UUID (FK sessions.games)
├── player_session_id UUID (FK sessions.players_in_game)
├── action_type ENUM (move, pass, swap, challenge, check)
├── action_payload JSONB (détails du coup)
├── executed_at TIMESTAMP UTC
├── result_status ENUM (success, failed, rejected)
├── error_message VARCHAR (nullable)
```

**Caractéristiques** :
- Nettoyage automatique après 7 jours d'inactivité (ou configurable)
- Pas d'archivage direct vers `history` (duplication intentionnelle)
- Index sur `game_id`, `player_session_id`, `updated_at`

---

### 2. `history` - Parties terminées (immuable, analytique)

Archive complète et immuable des parties jouées. Lecture seule après création.

**Tables** :

```
history.completed_games
├── game_id UUID PRIMARY KEY (même ID que sessions.games)
├── game_level ENUM
├── board_size INT
├── rack_size INT
├── min_word_length INT
├── language VARCHAR
├── queue ENUM
├── tournament_id UUID (nullable)
├── status ENUM (finished_normal, finished_by_player, abandoned)
├── created_at TIMESTAMP UTC
├── ended_at TIMESTAMP UTC
├── duration_seconds INT
├── winning_player_id UUID (FK rating.players, nullable)

history.game_participants
├── participant_id UUID PRIMARY KEY
├── game_id UUID (FK history.completed_games)
├── player_id UUID (FK rating.players)
├── nickname VARCHAR
├── final_score INT
├── rank INT (1, 2, 3... place)
├── was_host BOOLEAN

history.moves_log
├── move_id UUID PRIMARY KEY
├── game_id UUID (FK history.completed_games)
├── player_id UUID (FK rating.players)
├── move_number INT (ordre chrono)
├── action_type ENUM (move, pass, swap, challenge)
├── action_payload JSONB (coup joué)
├── board_after JSONB (état plateau après coup)
├── scores_after JSONB (scores après coup)
├── executed_at TIMESTAMP UTC

history.tournaments
├── tournament_id UUID PRIMARY KEY
├── name VARCHAR
├── start_date TIMESTAMP UTC
├── end_date TIMESTAMP UTC (nullable)
├── status ENUM (created, active, finished)
├── created_by_player_id UUID (FK rating.players)
```

**Caractéristiques** :
- Immutable après insertion (pas d'UPDATE ni DELETE)
- Archive complète pour audit et analytics
- Index sur `game_id`, `player_id`, `tournament_id`, `ended_at`

---

### 3. `rating` - Classements ELO (mises à jour, référentiel)

Notation ELO, statistiques joueur, et classements globaux.

**Tables** :

```
rating.players
├── player_id UUID PRIMARY KEY
├── username VARCHAR UNIQUE
├── created_at TIMESTAMP UTC

rating.player_ratings
├── rating_record_id UUID PRIMARY KEY
├── player_id UUID (FK rating.players, UNIQUE + queue)
├── queue ENUM (open, tournament, global)
├── elo_rating DECIMAL (1400.5, etc.)
├── games_played INT
├── games_won INT
├── games_lost INT
├── games_abandoned INT
├── total_points INT (somme scores)
├── avg_score DECIMAL
├── last_game_date TIMESTAMP UTC (nullable)
├── updated_at TIMESTAMP UTC

rating.leaderboard_snapshot
├── snapshot_id UUID PRIMARY KEY
├── queue ENUM
├── snapshot_date TIMESTAMP UTC
├── leaderboard_json JSONB (top 100 joueurs classés)
├── created_at TIMESTAMP UTC

rating.player_statistics
├── stats_id UUID PRIMARY KEY
├── player_id UUID (FK rating.players)
├── total_games_all_time INT
├── total_points_all_time INT
├── longest_winning_streak INT
├── favorite_game_level ENUM (ou NULL)
├── first_game_date TIMESTAMP UTC
├── last_game_date TIMESTAMP UTC
├── updated_at TIMESTAMP UTC
```

**Caractéristiques** :
- Mise à jour en temps réel après chaque partie terminée
- Snapshots de classement historiques (pour audit)
- Index sur `player_id`, `queue`, `elo_rating DESC`

---

## Relations Cross-Schema

```
sessions.games 
  ──PK──> history.completed_games (nightly batch ou à la fin partie)

sessions.players_in_game 
  ──FK──> rating.players (via player_id)

history.game_participants
  ──FK──> rating.players

history.completed_games
  ──FK──> history.tournaments (via tournament_id)

rating.player_ratings
  ←── mis à jour après history.completed_games
```

---

## Cycles de Vie

### Partie en cours (sessions → history)

```
1. [Sessions] CREATE game → sessions.games (status=created)
2. [Sessions] JOIN players → sessions.players_in_game
3. [Sessions] PLAY moves → sessions.turn_log (action_type=move, pass, swap, challenge)
4. [Sessions] UPDATE board/rack → sessions.board_state, sessions.rack_state
5. [Sessions] END game → sessions.games (status=ended)
6. [NIGHTLY] Batch transfer → history.completed_games + history.game_participants + history.moves_log
7. [RATING] Calculate ELO → rating.player_ratings (UPDATE)
8. [SESSIONS] Cleanup (DELETE old) → sessions.games WHERE updated_at < NOW() - 7 days
```

### Classement ELO mise à jour

```
1. Partie terminée → history.completed_games
2. Scores finaux calculés → history.game_participants
3. ELO calculé (algo Glicko2 simplifié)
4. rating.player_ratings UPDATE
5. rating.leaderboard_snapshot CREATE (hebdomadaire ou sur demande)
```

---

## Stratégie Multi-Environnement

### Dev (Docker)

**docker-compose.yml** :

```yaml
version: '3.8'
services:
  postgres-lama-dev:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: lama_dev
      POSTGRES_USER: lama_dev
      POSTGRES_PASSWORD: dev_password_change_me
    ports:
      - "5432:5432"
    volumes:
      - ./data/postgres:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/01-init.sql
      - ./scripts/seed-dev.sql:/docker-entrypoint-initdb.d/02-seed.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U lama_dev"]
      interval: 10s
      timeout: 5s
      retries: 5
```

**appsettings.Development.json** :

```json
{
  "ConnectionStrings": {
    "LamaServerDb": "Server=localhost;Port=5432;Database=lama_dev;User Id=lama_dev;Password=dev_password_change_me;",
    "LamaServerDb:Pooling": true,
    "LamaServerDb:MaxPoolSize": 10
  },
  "Database": {
    "AutoMigrate": true,
    "LogSql": true
  }
}
```

---

### Production

**appsettings.Production.json** :

```json
{
  "ConnectionStrings": {
    "LamaServerDb": "Server=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};User Id=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};SSL Mode=Require;"
  },
  "Database": {
    "AutoMigrate": false,
    "LogSql": false,
    "Replication": {
      "Enabled": true,
      "ReadReplicas": ["replica1.prod.local:5432"]
    }
  }
}
```

---

## Migrations EF Core

Structure proposée :

```
src/Server/Lama.Server/
├── Data/
│   ├── LamaDbContext.cs (DbContext principal)
│   ├── Migrations/
│   │   ├── 20260618_InitialSchemas.cs
│   │   ├── 20260618_CreateSessionsSchema.cs
│   │   ├── 20260618_CreateHistorySchema.cs
│   │   ├── 20260618_CreateRatingSchema.cs
│   │   └── ...
│   └── Models/
│       ├── Sessions/
│       │   ├── GameEntity
│       │   ├── PlayerInGameEntity
│       │   └── ...
│       ├── History/
│       │   ├── CompletedGameEntity
│       │   ├── GameParticipantEntity
│       │   └── ...
│       └── Rating/
│           ├── PlayerEntity
│           ├── PlayerRatingEntity
│           └── ...
```

---

## Points d'Attention

### 1. **Transactions Cross-Schema**

- Sessions → History : transfert asynchrone (pas transactionnel immédiat)
- Risque : perte de données si crash entre sessions.games.end et history.completed_games
- **Mitigation** : queue de transfert + retry avec idempotence

### 2. **Confidentialité / RGPD**

- Garder anonyme les joueurs publics en `history`
- Clé étrangère `rating.players.player_id` non exposée directement

### 3. **Scaling Futur**

- `sessions` peut être sur une BD séparée (volatile)
- `history` peut être archivée (data warehouse ou S3)
- `rating` peut avoir des réplicas (lecture seule)

### 4. **Nettoyage Sessions**

- TTL 7 jours sur parties non terminées (Cron job)
- À configurer selon usage réel

---

## Commandes de Gestion

```bash
# Dev : lancer le container PostgreSQL
docker compose -f docker-compose.dev.yml up -d

# Dev : appliquer les migrations
dotnet ef database update --project src/Server/Lama.Server --configuration Development

# Prod : générer script migration
dotnet ef migrations script --project src/Server/Lama.Server -o migrations.sql

# Prod : vérifier l'état de la BD
psql -h $POSTGRES_HOST -U $POSTGRES_USER -d $POSTGRES_DB -c "\dt"
```

---

## Prochaines Étapes (Implémentation)

1. **Setup EF Core**
   - Installer `Microsoft.EntityFrameworkCore.PostgreSQL`
   - Créer `LamaDbContext`
   - Configurer DbContext dans `Program.cs`

2. **Définir entités EF**
   - Sessions : GameEntity, PlayerInGameEntity, etc.
   - History : CompletedGameEntity, etc.
   - Rating : PlayerEntity, PlayerRatingEntity, etc.

3. **Migrations initiales**
   - `dotnet ef migrations add InitialSchemas`
   - Valider le SQL généré

4. **Adapter Program.cs**
   - Remplacer `GameHubState` (mémoire) par requêtes BD
   - Garder cache mémoire en plus (Redis optionnel)

5. **Batch transfers**
   - Service background `HistoryTransferService`
   - Cron job : partie non terminée → archive + ELO

6. **Tests + Documentation**
   - Tests intégration PostgreSQL
   - Doc déploiement production

---

## Références

- [Entity Framework Core PostgreSQL](https://learn.microsoft.com/en-us/ef/core/providers/postgresql)
- [PostgreSQL Schémas](https://www.postgresql.org/docs/current/ddl-schemas.html)
- [ELO Rating Glicko2](http://www.glicko.net/glicko/glicko2.pdf)
- `docs/multiplayer-migration-plan.md` (existant)

