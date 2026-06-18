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

        builder.Property(x => x.GameLevel).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Queue).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.Property(x => x.DurationSeconds).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.EndedAt).IsRequired();

        builder.HasIndex(x => x.EndedAt);
        builder.HasIndex(x => x.Queue);
    }
}

