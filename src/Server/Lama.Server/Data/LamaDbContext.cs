using Lama.Server.Data.Configurations;
using Lama.Server.Data.Models.History;
using Lama.Server.Data.Models.Rating;
using Lama.Server.Data.Models.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Data;

public sealed class LamaDbContext(DbContextOptions<LamaDbContext> options) : DbContext(options)
{
    public DbSet<SessionGameEntity> SessionGames => Set<SessionGameEntity>();
    public DbSet<SessionPlayerInGameEntity> SessionPlayersInGame => Set<SessionPlayerInGameEntity>();
    public DbSet<SessionTurnLogEntity> SessionTurnLogs => Set<SessionTurnLogEntity>();
    public DbSet<CompletedGameEntity> CompletedGames => Set<CompletedGameEntity>();
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<PlayerRatingEntity> PlayerRatings => Set<PlayerRatingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SessionGameEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SessionPlayerInGameEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SessionTurnLogEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CompletedGameEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlayerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlayerRatingEntityConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}

