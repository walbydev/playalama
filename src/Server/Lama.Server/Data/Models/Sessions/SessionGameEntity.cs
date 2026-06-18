namespace Lama.Server.Data.Models.Sessions;

public sealed class SessionGameEntity
{
    public Guid GameId { get; set; }
    public string GameLevel { get; set; } = "Standard";
    public int BoardSize { get; set; } = 15;
    public int RackSize { get; set; } = 7;
    public int MinWordLength { get; set; } = 2;
    public string Language { get; set; } = "fr";
    public string Queue { get; set; } = "open";
    public string Status { get; set; } = "created";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
}

