using Lama.Server.Data.Models.Rating;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class PlayerEntityConfiguration : IEntityTypeConfiguration<PlayerEntity>
{
    public void Configure(EntityTypeBuilder<PlayerEntity> builder)
    {
        builder.ToTable("players", "rating");

        builder.HasKey(x => x.PlayerId);
        builder.Property(x => x.PlayerId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();

        builder.Property(x => x.CreatedAt).IsRequired();
    }
}

