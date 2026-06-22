using Lama.Server.Data.Models.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class SessionBoardStateEntityConfiguration : IEntityTypeConfiguration<SessionBoardStateEntity>
{
    public void Configure(EntityTypeBuilder<SessionBoardStateEntity> builder)
    {
        builder.ToTable("board_state", "sessions");

        builder.HasKey(x => x.GameId);
        builder.Property(x => x.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(x => x.BoardJson).HasColumnName("board_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.UpdatedAt);
    }
}

