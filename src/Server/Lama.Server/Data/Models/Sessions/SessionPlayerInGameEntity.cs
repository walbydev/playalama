namespace Lama.Server.Data.Models.Sessions;

public sealed class SessionPlayerInGameEntity
{
    public Guid PlayerSessionId { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public int PlayerIndex { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

