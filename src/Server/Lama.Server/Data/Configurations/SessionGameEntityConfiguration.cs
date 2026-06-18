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
        builder.Property(x => x.GameId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.GameLevel).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Queue).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.UpdatedAt);
        builder.HasIndex(x => x.Queue);
    }
}

