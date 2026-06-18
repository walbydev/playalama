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
        builder.Property(x => x.RatingRecordId).HasColumnName("rating_record_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PlayerId).HasColumnName("player_id").IsRequired();
        builder.Property(x => x.Queue).HasColumnName("queue").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EloRating).HasColumnName("elo_rating").HasPrecision(8, 2).IsRequired();
        builder.Property(x => x.GamesPlayed).HasColumnName("games_played");
        builder.Property(x => x.GamesWon).HasColumnName("games_won");
        builder.Property(x => x.GamesLost).HasColumnName("games_lost");
        builder.Property(x => x.GamesAbandoned).HasColumnName("games_abandoned");
        builder.Property(x => x.TotalPoints).HasColumnName("total_points");
        builder.Property(x => x.AvgScore).HasColumnName("avg_score").HasPrecision(8, 2);
        builder.Property(x => x.LastGameDate).HasColumnName("last_game_date");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.PlayerId, x.Queue }).IsUnique();
        builder.HasIndex(x => new { x.Queue, x.EloRating });

        builder.HasOne(x => x.Player)
            .WithMany(x => x.Ratings)
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

