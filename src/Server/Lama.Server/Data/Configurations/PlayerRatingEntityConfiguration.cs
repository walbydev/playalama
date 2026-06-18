using Lama.Server.Data.Models.Rating;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class PlayerRatingEntityConfiguration : IEntityTypeConfiguration<PlayerRatingEntity>
{
    public void Configure(EntityTypeBuilder<PlayerRatingEntity> builder)
    {
        builder.ToTable("player_ratings", "rating");

        builder.HasKey(x => x.RatingRecordId);
        builder.Property(x => x.RatingRecordId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Queue).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EloRating).HasPrecision(8, 2).IsRequired();
        builder.Property(x => x.AvgScore).HasPrecision(8, 2);
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.PlayerId, x.Queue }).IsUnique();
        builder.HasIndex(x => new { x.Queue, x.EloRating });

        builder.HasOne(x => x.Player)
            .WithMany(x => x.Ratings)
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

