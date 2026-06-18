# LAMA Server - PostgreSQL EF Core Implementation Plan

**Date** : 2026-06-18  
**Status** : Planification (Prêt pour implémentation)  
**Priorité** : P0 (post audit)

## Vue d'ensemble

Plan détaillé pour intégrer Entity Framework Core + PostgreSQL dans Lama.Server, remplaçant l'état en mémoire actuel par une persistance multi-schéma.

## Phases d'implémentation

### Phase 1 : Setup EF Core (1-2 jours)

#### 1.1 Dépendances NuGet

```bash
cd src/Server/Lama.Server

# EF Core PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.PostgreSQL

# Outils migrations
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.Tools

# Optionnel : query builder fluent
dotnet add package Microsoft.EntityFrameworkCore.Relational
```

#### 1.2 Structure de fichiers

```
src/Server/Lama.Server/
├── Data/
│   ├── LamaDbContext.cs              (DbContext principal)
│   ├── DbContextOptions.cs           (Configuration EF Core)
│   ├── Migrations/
│   │   ├── 20260620_InitialSetup.Designer.cs
│   │   ├── 20260620_InitialSetup.cs
│   │   ├── 20260620_InitialSetup.resx
│   │   └── LamaDbContextModelSnapshot.cs
│   └── Models/
│       ├── Sessions/
│       │   ├── GameEntity.cs
│       │   ├── PlayerInGameEntity.cs
│       │   ├── BoardStateEntity.cs
│       │   ├── RackStateEntity.cs
│       │   ├── TurnLogEntity.cs
│       │   └── SessionConfigurations.cs  (IEntityTypeConfiguration)
│       ├── History/
│       │   ├── CompletedGameEntity.cs
│       │   ├── GameParticipantEntity.cs
│       │   ├── MovesLogEntity.cs
│       │   ├── TournamentEntity.cs
│       │   └── HistoryConfigurations.cs
│       └── Rating/
│           ├── PlayerEntity.cs
│           ├── PlayerRatingEntity.cs
│           ├── LeaderboardSnapshotEntity.cs
│           ├── PlayerStatisticsEntity.cs
│           ├── EloAdjustmentsLogEntity.cs
│           └── RatingConfigurations.cs
├── Repositories/  (Repository Pattern optionnel)
│   ├── IGameRepository.cs
│   ├── GameRepository.cs
│   ├── IHistoryRepository.cs
│   ├── HistoryRepository.cs
│   ├── IRatingRepository.cs
│   └── RatingRepository.cs
├── Services/
│   ├── SessionService.cs             (Gestion parties en cours)
│   ├── HistoryService.cs             (Archive et transfer)
│   ├── RatingService.cs              (ELO + leaderboards)
│   └── GamePersistenceService.cs     (Orchestration)
```

#### 1.3 LamaDbContext.cs

**Pseudo-code** :

```csharp
using Microsoft.EntityFrameworkCore;
using Lama.Server.Data.Models.Sessions;
using Lama.Server.Data.Models.History;
using Lama.Server.Data.Models.Rating;

namespace Lama.Server.Data;

public class LamaDbContext : DbContext
{
    // === Sessions (volatile) ===
    public DbSet<GameEntity> Games { get; set; }
    public DbSet<PlayerInGameEntity> PlayersInGame { get; set; }
    public DbSet<BoardStateEntity> BoardStates { get; set; }
    public DbSet<RackStateEntity> RackStates { get; set; }
    public DbSet<TurnLogEntity> TurnLogs { get; set; }

    // === History (archive) ===
    public DbSet<CompletedGameEntity> CompletedGames { get; set; }
    public DbSet<GameParticipantEntity> GameParticipants { get; set; }
    public DbSet<MovesLogEntity> MovesLogs { get; set; }
    public DbSet<TournamentEntity> Tournaments { get; set; }

    // === Rating ===
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<PlayerRatingEntity> PlayerRatings { get; set; }
    public DbSet<LeaderboardSnapshotEntity> LeaderboardSnapshots { get; set; }
    public DbSet<PlayerStatisticsEntity> PlayerStatistics { get; set; }
    public DbSet<EloAdjustmentsLogEntity> EloAdjustmentsLogs { get; set; }

    public LamaDbContext(DbContextOptions<LamaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Appliquer les configurations de chaque schéma
        modelBuilder.ApplyConfiguration(new SessionConfigurations());
        modelBuilder.ApplyConfiguration(new HistoryConfigurations());
        modelBuilder.ApplyConfiguration(new RatingConfigurations());

        // Utiliser les schémas PostgreSQL corrects
        modelBuilder.HasDefaultSchema(null);  // Pas de défaut global
        // Les configurations spécifient leur schéma via : modelBuilder.ToTable("games", "sessions")
    }
}
```

#### 1.4 Program.cs - Configuration DI

**À ajouter dans** `src/Server/Lama.Server/Program.cs` :

```csharp
// Après builder = WebApplication.CreateBuilder(args)

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("LamaServerDb")
    ?? throw new InvalidOperationException("ConnectionString not found");

builder.Services.AddDbContext<LamaDbContext>(options =>
    options
        .UseNpgsql(connectionString, npgOptions =>
        {
            npgOptions.MigrationsHistoryTable("__efmigrationshistory", "public");
            npgOptions.MigrationsAssembly("Lama.Server");
        })
        .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name })
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
);

// Repositories
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();

// Services
builder.Services.AddSingleton<GamePersistenceService>();
builder.Services.AddSingleton<HistoryService>();
builder.Services.AddSingleton<RatingService>();

// Automatically run migrations on startup (Dev only)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<DbMigrationService>();
}
```

---

### Phase 2 : Modèles d'entités EF Core (2-3 jours)

#### 2.1 Sessions - GameEntity.cs

**Exemple** :

```csharp
namespace Lama.Server.Data.Models.Sessions;

public class GameEntity
{
    public Guid GameId { get; set; }
    public string GameLevel { get; set; } = "Standard";  // Standard, Competitive, Tournament, Casual
    public int BoardSize { get; set; } = 15;
    public int RackSize { get; set; } = 7;
    public int MinWordLength { get; set; } = 2;
    public string Language { get; set; } = "fr";
    public string Queue { get; set; } = "open";  // open, tournament, global
    public Guid HostPlayerId { get; set; }
    public Guid? TournamentId { get; set; }
    public string Status { get; set; } = "created";  // created, active, paused, ended, abandoned
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }

    // Navigation properties
    public ICollection<PlayerInGameEntity> Players { get; set; } = [];
    public BoardStateEntity? BoardState { get; set; }
    public ICollection<RackStateEntity> RackStates { get; set; } = [];
    public ICollection<TurnLogEntity> TurnLogs { get; set; } = [];
}
```

**Configuration** (`SessionConfigurations.cs`) :

```csharp
// ...
modelBuilder.Entity<GameEntity>(entity =>
{
    entity.ToTable("games", "sessions");
    entity.HasKey(e => e.GameId);
    entity.Property(e => e.GameId).HasDefaultValueSql("gen_random_uuid()");

    // Indexes
    entity.HasIndex(e => e.Status);
    entity.HasIndex(e => e.UpdatedAt);
    entity.HasIndex(e => e.Queue);

    // Constraints
    entity.Property(e => e.GameLevel).IsRequired();
    entity.Property(e => e.Status).IsRequired();

    // Relations
    entity.HasMany(e => e.Players)
        .WithOne(p => p.Game)
        .HasForeignKey(p => p.GameId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

#### 2.2 Repositories Pattern (optionnel mais recommandé)

**Exemple** : `IGameRepository.cs`

```csharp
namespace Lama.Server.Data.Repositories;

public interface IGameRepository
{
    Task<GameEntity?> GetByIdAsync(Guid gameId);
    Task<List<GameEntity>> GetActiveGamesAsync();
    Task<List<GameEntity>> GetGamesByPlayerAsync(Guid playerId);
    Task AddAsync(GameEntity game);
    Task UpdateAsync(GameEntity game);
    Task SaveChangesAsync();
}

public class GameRepository : IGameRepository
{
    private readonly LamaDbContext _context;

    public GameRepository(LamaDbContext context)
    {
        _context = context;
    }

    public async Task<GameEntity?> GetByIdAsync(Guid gameId)
        => await _context.Games
            .Include(g => g.Players)
            .Include(g => g.BoardState)
            .FirstOrDefaultAsync(g => g.GameId == gameId);

    public async Task<List<GameEntity>> GetActiveGamesAsync()
        => await _context.Games
            .Where(g => g.Status.In("created", "active", "paused"))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

    // ...
}
```

---

### Phase 3 : Migrations EF Core (1 jour)

#### 3.1 Créer la migration initiale

```bash
cd src/Server/Lama.Server

# Générer la migration basée sur les modèles
dotnet ef migrations add InitialSetup --context LamaDbContext

# Vérifier le SQL généré
dotnet ef migrations script --context LamaDbContext --output migrations/20260620_InitialSetup.sql
```

#### 3.2 Valider le SQL

Examiner `migrations/20260620_InitialSetup.sql` pour verser que :
- Les 3 schémas (`sessions`, `history`, `rating`) sont créés
- Les constraints et indexes sont corrects
- Les foreign keys sont déclarées

#### 3.3 Appliquer la migration (Dev)

```bash
# En dev, les migrations auto-run via DbMigrationService
dotnet run --project src/Server/Lama.Server --configuration Development

# Ou manuellement
dotnet ef database update --context LamaDbContext --configuration Development
```

---

### Phase 4 : Adapter Program.cs et endpoints (2-3 jours)

#### 4.1 Remplacer GameHubState → DB

**Avant** (en mémoire) :

```csharp
var state = GameHubState.Instance;
var game = state.Get(gameId);
```

**Après** (BD persistante) :

```csharp
var repository = context.RequestServices.GetRequiredService<IGameRepository>();
var game = await repository.GetByIdAsync(gameId);
if (game == null) return Results.NotFound();
```

#### 4.2 Async/await pour tous les endpoints

All `app.MapPost`, `app.MapGet` doivent devenir `async` :

```csharp
app.MapPost("/api/games/{gameId}/moves", async (
    string gameId,
    MoveRequest request,
    IGameRepository gameRepo,
    IRatingRepository ratingRepo) =>
{
    var game = await gameRepo.GetByIdAsync(Guid.Parse(gameId));
    if (game == null) return Results.NotFound();

    // Exécuter le coup
    var result = await game.ExecuteMoveAsync(request);
    
    // Persister
    await gameRepo.UpdateAsync(game);
    await gameRepo.SaveChangesAsync();

    return Results.Ok(result);
});
```

---

### Phase 5 : Services métier (historique, rating) (3-4 jours)

#### 5.1 HistoryService - Transfert sessions → history

```csharp
namespace Lama.Server.Services;

public class HistoryService
{
    private readonly LamaDbContext _context;
    private readonly ILogger<HistoryService> _logger;

    public HistoryService(LamaDbContext context, ILogger<HistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Transfère une partie terminée de sessions vers history
    /// </summary>
    public async Task ArchiveCompletedGameAsync(Guid gameId)
    {
        var sessionGame = await _context.Games
            .Where(g => g.GameId == gameId && g.Status == "ended")
            .FirstOrDefaultAsync();

        if (sessionGame == null)
            throw new InvalidOperationException($"Game {gameId} not found or not ended");

        // 1. Créer CompletedGame dans history
        var completedGame = new CompletedGameEntity
        {
            GameId = sessionGame.GameId,
            GameLevel = sessionGame.GameLevel,
            // ... copier autres champs
            DurationSeconds = (int)(sessionGame.EndedAt - sessionGame.CreatedAt!.Value).TotalSeconds
        };

        _context.CompletedGames.Add(completedGame);

        // 2. Copier participants
        var participants = sessionGame.Players.Select(p => new GameParticipantEntity
        {
            GameId = gameId,
            PlayerId = p.PlayerId,
            FinalScore = p.FinalScore // TODO: récupérer depuis le moteur de jeu
            // ...
        }).ToList();

        _context.GameParticipants.AddRange(participants);

        // 3. Copier les moves
        var moves = await _context.TurnLogs
            .Where(t => t.GameId == gameId)
            .ToListAsync();

        var movesLogs = moves.Select(m => new MovesLogEntity
        {
            GameId = gameId,
            PlayerId = m.PlayerId,
            MoveNumber = m.TurnNumber,
            ActionType = m.ActionType,
            // ...
        }).ToList();

        _context.MovesLogs.AddRange(movesLogs);

        // 4. Sauvegarder
        await _context.SaveChangesAsync();

        // 5. Optionnel : Supprimer de sessions (après vérification)
        _context.Games.Remove(sessionGame);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Game {GameId} archived to history", gameId);
    }
}
```

#### 5.2 RatingService - Calcul ELO

```csharp
namespace Lama.Server.Services;

public class RatingService
{
    private readonly LamaDbContext _context;

    public async Task UpdateRatingsAfterGameAsync(Guid gameId)
    {
        var completedGame = await _context.CompletedGames
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.GameId == gameId);

        if (completedGame == null) return;

        foreach (var participant in completedGame.Participants)
        {
            var eloChange = CalculateEloChange(participant.Rank, participant.FinalScore);
            
            // Appeler la stored procedure PostgreSQL
            await _context.Database.ExecuteSqlAsync(
                "SELECT rating.update_elo({0}, {1}, {2}, {3}, {4})",
                participant.PlayerId,
                completedGame.Queue,
                eloChange,
                $"{participant.Rank == 1 ? "win" : "loss"}",
                gameId
            );
        }
    }

    private static decimal CalculateEloChange(int rank, int score)
    {
        // Simplifié : Glicko2 ultérieur
        return rank == 1 ? 16m : rank == 2 ? 0m : -16m;
    }
}
```

---

### Phase 6 : Background Jobs et Cleanup (1-2 jours)

#### 6.1 Hosted Service - Nettoyage sessions obsolètes

```csharp
namespace Lama.Server.Services;

public class SessionCleanupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private ILogger<SessionCleanupService> _logger;
    private Timer? _timer;

    public SessionCleanupService(IServiceProvider serviceProvider, ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(async _ => await CleanupAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
        return Task.CompletedTask;
    }

    private async Task CleanupAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LamaDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var oldGames = await context.Games
            .Where(g => g.UpdatedAt < cutoff && g.Status != "active")
            .ToListAsync();

        if (oldGames.Count > 0)
        {
            context.Games.RemoveRange(oldGames);
            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} old game sessions", oldGames.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }
}
```

#### 6.2 LeaderboardSnapshotService

```csharp
// Service hebdomadaire pour générer snapshots de classements
public class LeaderboardSnapshotService : IHostedService
{
    // ...
    // Similaire à SessionCleanupService mais avec une fréquence hebdomadaire
}
```

---

### Phase 7 : Tests d'intégration (1-2 jours)

#### 7.1 Test PostgreSQL localement

```bash
# Démarrer PostgreSQL
docker compose -f docker-compose.postgresdev.yml up -d

# Lancer les tests
dotnet test tests/Lama.Server.Tests/Lama.Server.Tests.csproj
```

#### 7.2 Test unitaires recommandés

- `GameRepositoryTests` : CRUD opérations
- `HistoryServiceTests` : Transfert sessions → history
- `RatingServiceTests` : Calculs ELO
- `DbContextTests` : Migrations, constraints

---

## Timeline estimée

| Phase | Durée estimée | Notes |
|-------|---|---|
| Phase 1 : Setup EF Core | 1-2 jours | Infrastructure + dépendances |
| Phase 2 : Modèles d'entités | 2-3 jours | 15+ entités + configurations |
| Phase 3 : Migrations | 1 jour | SQL gen + validation |
| Phase 4 : Adapter endpoints | 2-3 jours | Async/await + BD calls |
| Phase 5 : Services métier | 3-4 jours | Histoire + Rating logique |
| Phase 6 : Background jobs | 1-2 jours | Cleanup + snapshots |
| Phase 7 : Tests | 1-2 jours | QA + validation |
| **Total** | **11-17 jours** | ~2-3 semaines en parallèle |

---

## Points de vigilance

1. **Transactions cross-schema** : Sessions → History → Rating peut être asynchrone (queue pattern)
2. **Performances** : Index sur `sessions.games.updated_at`, `rating.player_ratings.elo_rating DESC`
3. **Backward compatibility** : Garder `GameHubState` optionnel pour cache en-mémoire (Redis futur)
4. **Retries** : Network failures, connection pooling
5. **Secrets** : Passwords PostgreSQL ne doivent JAMAIS être en appsettings.json en prod

---

## Ressources

- [EF Core PostgreSQL](https://learn.microsoft.com/en-us/ef/core/providers/postgresql)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations)
- [PostgreSQL Architecture](./POSTGRESQL_ARCHITECTURE.md)
- [Quick Start](./POSTGRESQL_QUICKSTART.md)

