using Lama.Server.Data.Models.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class SessionGameEntityConfiguration : IEntityTypeConfiguration<SessionGameEntity>
{
    public void Configure(EntityTypeBuilder<SessionGameEntity> builder)
    {
        builder.ToTable("games", "sessions");

        builder.HasKey(x => x.GameId);
        builder.Property(x => x.GameId).HasColumnName("game_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.GameLevel).HasColumnName("game_level").HasMaxLength(50).IsRequired();
        builder.Property(x => x.BoardSize).HasColumnName("board_size").IsRequired();
        builder.Property(x => x.RackSize).HasColumnName("rack_size").IsRequired();
        builder.Property(x => x.MinWordLength).HasColumnName("min_word_length").IsRequired();
        builder.Property(x => x.Language).HasColumnName("language").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Queue).HasColumnName("queue").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.UpdatedAt);
        builder.HasIndex(x => x.Queue);
    }
}

