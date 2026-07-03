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
    string? TournamentId = null,
    OnlineGameMode? Mode = null,
    string? GameName = null,
    bool IsPrivate = false,
    string? Password = null,
    int? MaxPlayers = null,
    bool EnableAi = false,
    /// <summary>
    /// Identifiant du bot à utiliser (ex: "bot-karim"). Prioritaire sur <see cref="EnableAi"/>.
    /// Si null et <see cref="EnableAi"/> est true, le bot par défaut (Karim) est sélectionné.
    /// </summary>
    string? AiBotId = null,
    /// <summary>
    /// Liste explicite des bots à injecter (0..3 en mode humain+IA, IDs uniques).
    /// Si renseignée, prioritaire sur <see cref="AiBotId"/>.
    /// </summary>
    IReadOnlyList<string>? AiBotIds = null,
    /// <summary>
    /// Nombre de bots IA à injecter dans la partie.
    /// 0..3 avec humain, 1..4 en mode IA-only.
    /// </summary>
    int? AiBotCount = null,
    /// <summary>
    /// Inclut le créateur humain parmi les participants.
    /// false = partie 100% IA (admin uniquement).
    /// </summary>
    bool? IncludeHost = null,
    /// <summary>
    /// Langues de jeu (mots du plateau). Un mot est valide s'il existe dans au moins une.
    /// Si vide, <see cref="Language"/> est utilisé.
    /// </summary>
    IReadOnlyList<string>? Languages = null,
    /// <summary>Temps alloué par joueur en secondes en mode Blitz (null sinon).</summary>
    int? TimePerPlayerSeconds = null);

public sealed record JoinGameRequest(string PlayerName, string? Password = null);

public sealed record EndGameRequest(string? PlayerId);

public sealed record AbandonGameRequest(string? PlayerId);

public sealed record StartGameRequest(string? PlayerId, bool Force = false);

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
    IGameEngine Engine,
    OnlineGameMode Mode = OnlineGameMode.Solo,
    string? GameName = null,
    bool IsPrivate = false,
    string? PasswordHash = null,
    int MaxPlayers = 1,
    int ReservedAiSlots = 0,
    bool HasStarted = true,
    bool UsesLobby = false,
    bool IsClosed = false)
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
    public OnlineGameMode Mode { get; } = Mode;
    public string? GameName { get; } = GameName;
    public bool IsPrivate { get; } = IsPrivate;
    public string? PasswordHash { get; } = PasswordHash;
    public int MaxPlayers { get; } = MaxPlayers;
    public int ReservedAiSlots { get; } = ReservedAiSlots;
    public bool HasStarted { get; set; } = HasStarted;
    public bool UsesLobby { get; } = UsesLobby;
    public bool IsClosed { get; set; } = IsClosed;
    /// <summary>IDs des joueurs qui ont abandonné (mais la partie continue pour les autres).</summary>
    public HashSet<string> AbandonedPlayerIds { get; } = [];
    /// <summary>Raison de fin de partie : "abandoned" si un joueur a forcé la fin.</summary>
    public string? EndReason { get; set; }
    /// <summary>Nom du joueur à l'origine de la fin prématurée.</summary>
    public string? AbandonedByName { get; set; }
    /// <summary>Indique qu'au moins une suggestion a été utilisée pendant la partie
    /// (l'Elo n'est alors pas approvisionné, hors mode Tournament).</summary>
    public bool SuggestionsUsed { get; set; }
    /// <summary>Temps alloué par joueur en secondes en mode Blitz (null sinon).</summary>
    public int? TimePerPlayerSeconds { get; set; }
}

public sealed record OnlinePlayer(string PlayerId, string PlayerName, bool IsHost, bool IsBot = false);

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
    string Source,
    OnlineGameMode Mode = OnlineGameMode.Solo,
    string? GameName = null,
    bool IsPrivate = false,
    bool IsJoinable = true,
    int MaxPlayers = 1,
    int ReservedAiSlots = 0,
    bool IsClosed = false);

public enum OnlineGameMode
{
    Solo = 0,
    Multi = 1
}

public sealed record OnlineScoreEntry(string PlayerName, int Score);
public sealed record OnlineBoardTile(int Row, int Column, char Letter, bool IsWildcard);
public sealed record OnlineMovePlacement(int Row, int Column, char Letter);

public sealed record ServerEvent(string Type, object Payload);
