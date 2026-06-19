using System.Text.Json;
using Lama.Contracts;
using Lama.Domain.Engine;

namespace Lama.Server.Contracts.Api;

public sealed record CreateGameRequest(
    string HostName,
    GameLevel? GameLevel = null,
    int BoardSize = 15,
    int RackSize = 7,
    int MinWordLength = 2,
    string Language = "fr",
    string? TournamentId = null);

public sealed record JoinGameRequest(string PlayerName);

public sealed record EndGameRequest(string? PlayerId);

public sealed record PlayMoveRequest(string PlayerId, string Command, JsonElement? Payload = null);

public sealed class OnlineGame(
    string Id,
    GameLevel GameLevel,
    int BoardSize,
    int RackSize,
    int MinWordLength,
    string Language,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<OnlinePlayer> Players,
    Dictionary<string, int> PlayerIndexById,
    List<OnlineMove> Moves,
    string? TournamentId,
    RankingQueue Queue,
    IGameEngine Engine)
{
    public string Id { get; } = Id;
    public GameLevel GameLevel { get; } = GameLevel;
    public int BoardSize { get; } = BoardSize;
    public int RackSize { get; } = RackSize;
    public int MinWordLength { get; } = MinWordLength;
    public string Language { get; } = Language;
    public DateTimeOffset CreatedAt { get; } = CreatedAt;
    public DateTimeOffset UpdatedAt { get; set; } = UpdatedAt;
    public List<OnlinePlayer> Players { get; } = Players;
    public Dictionary<string, int> PlayerIndexById { get; } = PlayerIndexById;
    public List<OnlineMove> Moves { get; } = Moves;
    public string? TournamentId { get; } = TournamentId;
    public RankingQueue Queue { get; } = Queue;
    public IGameEngine Engine { get; } = Engine;
}

public sealed record OnlinePlayer(string PlayerId, string PlayerName, bool IsHost);

public sealed record OnlineMove(
    string MoveId,
    string PlayerId,
    string PlayerName,
    string Command,
    JsonElement? Payload,
    DateTimeOffset PlayedAt,
    int TurnNumber,
    IReadOnlyList<OnlineMovePlacement> Placements,
    int Score = 0);

public sealed record OnlineGameListItem(
    string Id,
    GameLevel GameLevel,
    RankingQueue Queue,
    int BoardSize,
    int RackSize,
    int MinWordLength,
    string Language,
    string Status,
    bool IsGameOver,
    int Players,
    int Moves,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Source);

public sealed record OnlineScoreEntry(string PlayerName, int Score);
public sealed record OnlineBoardTile(int Row, int Column, char Letter, bool IsWildcard);
public sealed record OnlineMovePlacement(int Row, int Column, char Letter);

public sealed record ServerEvent(string Type, object Payload);

