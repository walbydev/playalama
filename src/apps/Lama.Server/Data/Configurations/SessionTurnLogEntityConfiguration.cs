using Lama.Server.Data.Models.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lama.Server.Data.Configurations;

public sealed class SessionTurnLogEntityConfiguration : IEntityTypeConfiguration<SessionTurnLogEntity>
{
    public void Configure(EntityTypeBuilder<SessionTurnLogEntity> builder)
    {
        builder.ToTable("turn_log", "sessions");

        builder.HasKey(x => x.TurnId);
        builder.Property(x => x.TurnId).HasColumnName("turn_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(x => x.PlayerSessionId).HasColumnName("player_session_id").IsRequired();
        builder.Property(x => x.TurnNumber).HasColumnName("turn_number").IsRequired();
        builder.Property(x => x.ActionType).HasColumnName("action_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ActionPayload).HasColumnName("action_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExecutedAt).HasColumnName("executed_at").IsRequired();
        builder.Property(x => x.ResultStatus).HasColumnName("result_status").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");

        builder.HasIndex(x => x.GameId);
        builder.HasIndex(x => x.PlayerSessionId);
        builder.HasIndex(x => x.ExecutedAt);
        builder.HasIndex(x => new { x.GameId, x.TurnNumber });
    }
}

