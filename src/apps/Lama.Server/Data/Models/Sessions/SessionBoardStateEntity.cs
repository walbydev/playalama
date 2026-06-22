namespace Lama.Server.Data.Models.Sessions;

public sealed class SessionBoardStateEntity
{
    public Guid GameId { get; set; }
    public string BoardJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

