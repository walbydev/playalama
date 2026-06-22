using Lama.Server.Data.Models.History;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class CompletedGameEntityConfiguration : IEntityTypeConfiguration<CompletedGameEntity>
{
    public void Configure(EntityTypeBuilder<CompletedGameEntity> builder)
    {
        builder.ToTable("completed_games", "history");

        builder.HasKey(x => x.GameId);
        builder.Property(x => x.GameId).HasColumnName("game_id");

        builder.Property(x => x.GameLevel).HasColumnName("game_level").HasMaxLength(50).IsRequired();
        builder.Property(x => x.BoardSize).HasColumnName("board_size").IsRequired();
        builder.Property(x => x.RackSize).HasColumnName("rack_size").IsRequired();
        builder.Property(x => x.MinWordLength).HasColumnName("min_word_length").IsRequired();
        builder.Property(x => x.Language).HasColumnName("language").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Queue).HasColumnName("queue").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.EndedAt).HasColumnName("ended_at").IsRequired();
        builder.Property(x => x.DurationSeconds).HasColumnName("duration_seconds").IsRequired();
        builder.Property(x => x.WinningPlayerId).HasColumnName("winning_player_id");

        builder.HasIndex(x => x.EndedAt);
        builder.HasIndex(x => x.Queue);
    }
}

