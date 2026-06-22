namespace Lama.Server.Data.Models.Sessions;

public sealed class SessionTurnLogEntity
{
    public Guid TurnId { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerSessionId { get; set; }
    public int TurnNumber { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string ActionPayload { get; set; } = "{}";
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ResultStatus { get; set; } = "success";
    public string? ErrorMessage { get; set; }
}

