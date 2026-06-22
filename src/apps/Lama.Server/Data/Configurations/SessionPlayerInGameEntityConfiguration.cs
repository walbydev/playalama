using Lama.Server.Data.Models.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class SessionPlayerInGameEntityConfiguration : IEntityTypeConfiguration<SessionPlayerInGameEntity>
{
    public void Configure(EntityTypeBuilder<SessionPlayerInGameEntity> builder)
    {
        builder.ToTable("players_in_game", "sessions");

        builder.HasKey(x => x.PlayerSessionId);
        builder.Property(x => x.PlayerSessionId).HasColumnName("player_session_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(x => x.PlayerId).HasColumnName("player_id").IsRequired();
        builder.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsHost).HasColumnName("is_host").IsRequired();
        builder.Property(x => x.PlayerIndex).HasColumnName("player_index").IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").IsRequired();

        builder.HasIndex(x => x.GameId);
        builder.HasIndex(x => x.PlayerId);
        builder.HasIndex(x => x.IsHost);
        builder.HasIndex(x => new { x.GameId, x.PlayerId }).IsUnique();
        builder.HasIndex(x => new { x.GameId, x.PlayerIndex }).IsUnique();
    }
}

