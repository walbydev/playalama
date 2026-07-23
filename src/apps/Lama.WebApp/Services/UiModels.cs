using System.ComponentModel.DataAnnotations;

namespace Lama.WebApp.Services;

// ── Formulaires ───────────────────────────────────────────────────────────────

public sealed class CreateGameForm
{
    public string Mode { get; set; } = "multi";
    public string? GameName { get; set; }
    public int MaxPlayers { get; set; } = 4;
    /// <summary>Bot sélectionné par slot. Index 0 = slot solo ; 0..3 = slots mode IA-only.</summary>
    public List<string?> AiBotIds { get; set; } = new() { null, null, null, null };
    /// <summary>Langues de jeu (plateau). Union : un mot valide dans ≥1 langue.</summary>
    public List<string> Languages { get; set; } = new() { "fr" };
    /// <summary>True si mode Blitz activé.</summary>
    public bool IsBlitz { get; set; }
    /// <summary>Temps par joueur en minutes en mode Blitz (5, 10 ou 25).</summary>
    public int BlitzTimeMinutes { get; set; } = 10;
}

public sealed class PlayForm
{
    public string PlayerId { get; set; } = string.Empty;
    public string Command { get; set; } = "play.pass";
    public string Position { get; set; } = "H8";
    public string Word { get; set; } = string.Empty;
    public string Direction { get; set; } = "H";
    /// <summary>Placements visuels (drag-and-drop). Prioritaire sur Position/Word/Direction.</summary>
    public List<PlacementDto>? Placements { get; set; }
    /// <summary>Échanger toutes les lettres du rack (play.swap).</summary>
    public bool SwapAll { get; set; } = false;
}

/// <summary>Placement d'une tuile : ligne/colonne/lettre (minuscule = joker).</summary>
public sealed record PlacementDto(int Row, int Col, char Letter);

public sealed class LoginForm
{
    [Required(ErrorMessage = "Le pseudo est requis.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterForm
{
    [Required(ErrorMessage = "Le pseudo est requis.")]
    [MinLength(2, ErrorMessage = "Le pseudo doit contenir au moins 2 caractères.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [MinLength(6, ErrorMessage = "Le mot de passe doit contenir au moins 6 caractères.")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string PasswordConfirm { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? CountryCode { get; set; }
}

public sealed class ProfileUpdateForm
{
    public string? Email { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string? NewPasswordConfirm { get; set; }
    public string? CountryCode { get; set; }
}

// ── Réponses API ──────────────────────────────────────────────────────────────

public sealed record WebAuthResponse(string Token, string PlayerId, string PlayerName, string? Email, string? CountryCode, bool IsAdmin, DateTime ExpiresAt);
public sealed record WebCreateGameResponse(string GameId, string HostPlayerId);
public sealed record WebJoinGameResponse(string GameId, string PlayerId, List<char>? Rack = null);

public sealed record WebGameListItem(
    string Id,
    string? GameName,
    string Status,
    int Players,
    int MaxPlayers,
    string Queue,
    bool IsJoinable);

public sealed record WebGameSnapshot(
    string Id,
    string? GameName,
    bool IsGameOver,
    bool HasStarted,
    bool UsesLobby,
    bool IsClosed,
    int CurrentPlayerIndex,
    int TurnNumber,
    int BagCount,
    int MaxPlayers,
    int BoardSize,
    int RackSize,
    string Language,
    IReadOnlyList<WebSnapshotPlayer> Players,
    IReadOnlyList<WebBoardTile> Board,
    string? LastMoveId,
    string? LastMovePlayerName,
    int? LastMoveTurnNumber,
    string? LastMoveCommand,
    int? LastMoveScore,
    IReadOnlyList<string> AbandonedPlayerIds,
    string? EndReason,
    string? AbandonedByName,
    int? TimePerPlayerSeconds = null,
    int? ForfeitedPlayerIndex = null,
    DateTimeOffset? TurnStartAt = null);

public sealed record WebSnapshotPlayer(string PlayerId, string PlayerName, int Score, bool IsHost, IReadOnlyList<char> Rack, int RackCount, bool IsBot = false, int TimeUsed = 0);
public sealed record WebBoardTile(int Row, int Column, char Letter);
public sealed record WebPlayResponse(string GameId, string MoveId, int Score);
public sealed record WebCheckResponse(int Score, string Message);
public sealed record WebWordInfo(string Word, string Lang, string? WiktionaryUrl, IReadOnlyList<WebWordDefinition> Definitions, IReadOnlyList<string> Synonyms);
public sealed record WebWordDefinition(int SenseIndex, string? PartOfSpeech, string Text);
public sealed record WebSuggestedMove(string Word, string Position, string Direction, int Score, int Length, double BalancedScore, string Category);

public sealed record WebAccessibilityPreferences(string Theme, int FontSize, double BoardScale);
public sealed record WebPlayerProfile(string PlayerId, string Username, string? Email, string? CountryCode, WebAccessibilityPreferences? Accessibility, DateTimeOffset CreatedAt);

public sealed record WebGameHistoryItem(
    string GameId,
    string GameLevel,
    string Queue,
    string Status,
    DateTimeOffset EndedAt,
    int DurationSeconds,
    bool IsWinner);

// ── Bots IA ───────────────────────────────────────────────────────────────────

public sealed record WebStatsDto(int ActivePlayers, int GamesPlayed, int Languages);

public sealed record WebBotDto(string BotId, string Name, int Level, int InitialElo)
{
    public string LevelLabel => Level switch
    {
        1 => "Débutant",
        2 => "Intermédiaire",
        3 => "Avancé",
        4 => "Expert",
        5 => "Légendaire",
        _ => $"Niveau {Level}"
    };
    public string LevelEmoji => Level switch
    {
        1 => "🌱",
        2 => "🎓",
        3 => "⚔️",
        4 => "🏆",
        5 => "👑",
        _ => "🤖"
    };
}

// ── Classements ───────────────────────────────────────────────────────────────

public sealed record LeaderboardEntry(
    int Rank,
    string PlayerId,
    string Username,
    string? CountryCode,
    int Level,
    int Elo,
    int Wins,
    int Games);

// ── Rating joueur ──────────────────────────────────────────────────────────────

public sealed record WebPlayerRating(
    string PlayerId,
    string Username,
    string LevelName,
    int Level,
    int EloOpen,
    int EloTournament,
    double GlobalPrestige,
    int Wins,
    int Losses,
    int Abandoned,
    double WinRate,
    double AverageScore,
    DateTimeOffset? LastGameAt)
{
    public int TotalGames => Wins + Losses + Abandoned;

    /// <summary>Emoji du niveau (extrait du LevelName qui commence par l'emoji).</summary>
    public string LevelEmoji => LevelName.Length > 0
        ? string.Concat(LevelName.TakeWhile(c => !char.IsLetter(c))).Trim()
        : "✨";
}

// ── Monitoring / Dashboard ─────────────────────────────────────────────────

public sealed record ServerStatusDto(
    DateTimeOffset CollectedAt,
    ServerSectionDto Server,
    GamesSectionDto Games,
    PlayersSectionDto Players,
    HistorySectionDto History,
    DatabaseSectionDto Database,
    AiServerSectionDto AiServer
);

public sealed record ServerSectionDto(
    string Uptime,
    long UptimeSeconds,
    double MemoryMb,
    int ThreadCount,
    string Environment,
    string Version
);

public sealed record GamesSectionDto(
    int ActiveCount,
    int ActivePlayers,
    bool IsDraining = false
);

public sealed record PlayersSectionDto(
    int TotalRegistered,
    int RegisteredToday
);

public sealed record HistorySectionDto(
    int TotalCompleted,
    int CompletedToday,
    int TotalSessions,
    int SessionsToday
);

public sealed record DatabaseSectionDto(
    string Status,
    string Provider,
    bool MigrationPending
);

public sealed record AiServerSectionDto(
    string Status,
    string? Url,
    string? Language,
    int? ResponseTimeMs
);

// ── Admin: Users management ──────────────────────────────────────────────────

public sealed record AdminUserListResponse(int Total, List<AdminUserDto> Users);

public sealed record AdminUserDto(
    Guid PlayerId,
    string Username,
    string? Email,
    string? CountryCode,
    bool IsAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    decimal EloRating,
    int GamesPlayed,
    int GamesWon,
    bool IsConnected
);

// ── Admin: Games management ──────────────────────────────────────────────────

public sealed record AdminGameListResponse(int Total, List<AdminGameDto> Games);

public sealed record AdminGameDto(
    string GameId,
    string GameLevel,
    string Queue,
    int BoardSize,
    int RackSize,
    string Language,
    string Status,
    bool IsGameOver,
    int PlayerCount,
    int MoveCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Source,
    List<AdminGamePlayerDto> Players
);

public sealed record AdminGamePlayerDto(string PlayerName, bool IsBot);

// ── Admin: Drain (maintenance) ───────────────────────────────────────────────

public sealed record DrainStatusDto(bool IsDraining, int ActiveGames);

public static class CountryList
{
    public static readonly IReadOnlyList<(string Code, string Flag)> All = new[]
    {
        ("FR", "🇫🇷"), ("BE", "🇧🇪"), ("CH", "🇨🇭"), ("CA", "🇨🇦"),
        ("LU", "🇱🇺"), ("MC", "🇲🇨"), ("SN", "🇸🇳"), ("MA", "🇲🇦"),
        ("TN", "🇹🇳"), ("DZ", "🇩🇿"), ("CI", "🇨🇮"), ("CM", "🇨🇲"),
        ("US", "🇺🇸"), ("GB", "🇬🇧"), ("DE", "🇩🇪"), ("ES", "🇪🇸"),
        ("IT", "🇮🇹"), ("PT", "🇵🇹"), ("NL", "🇳🇱"), ("BR", "🇧🇷"),
        ("MX", "🇲🇽"), ("AR", "🇦🇷"), ("JP", "🇯🇵"), ("KR", "🇰🇷"),
        ("CN", "🇨🇳"), ("IN", "🇮🇳"), ("AU", "🇦🇺"), ("PL", "🇵🇱"),
        ("RU", "🇷🇺"), ("UA", "🇺🇦"),
    };

    public static string FlagFor(string? code) =>
        All.FirstOrDefault(c => c.Code == code).Flag ?? string.Empty;
}
