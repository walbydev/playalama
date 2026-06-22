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
        builder.Property(x => x.PlayerId).HasColumnName("player_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();

        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(256).IsRequired(false);
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("email IS NOT NULL");

        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired(false);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

