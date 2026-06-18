namespace Lama.Server.Data.Models.History;

public sealed class CompletedGameEntity
{
    public Guid GameId { get; set; }
    public string GameLevel { get; set; } = "Standard";
    public int BoardSize { get; set; } = 15;
    public int RackSize { get; set; } = 7;
    public int MinWordLength { get; set; } = 2;
    public string Language { get; set; } = "fr";
    public string Queue { get; set; } = "open";
    public string Status { get; set; } = "finished_normal";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int DurationSeconds { get; set; }
    public Guid? WinningPlayerId { get; set; }
}

