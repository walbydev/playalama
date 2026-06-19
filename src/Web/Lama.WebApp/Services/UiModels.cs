using System.ComponentModel.DataAnnotations;

namespace Lama.WebApp.Services;

// ── Formulaires ───────────────────────────────────────────────────────────────

public sealed class CreateGameForm
{
    public string Mode { get; set; } = "multi";
    public string? GameName { get; set; }
    public int MaxPlayers { get; set; } = 4;
}

public sealed class PlayForm
{
    public string PlayerId { get; set; } = string.Empty;
    public string Command { get; set; } = "play.pass";
    public string Position { get; set; } = "H8";
    public string Word { get; set; } = string.Empty;
    public string Direction { get; set; } = "H";
}

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
}

public sealed class ProfileUpdateForm
{
    public string? Email { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string? NewPasswordConfirm { get; set; }
}

// ── Réponses API ──────────────────────────────────────────────────────────────

public sealed record WebAuthResponse(string Token, string PlayerId, string PlayerName, string? Email, DateTime ExpiresAt);
public sealed record WebCreateGameResponse(string GameId, string HostPlayerId);
public sealed record WebJoinGameResponse(string GameId, string PlayerId);

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
    bool IsGameOver,
    bool HasStarted,
    bool UsesLobby,
    int CurrentPlayerIndex,
    int TurnNumber,
    IReadOnlyList<WebSnapshotPlayer> Players,
    IReadOnlyList<WebBoardTile> Board);

public sealed record WebSnapshotPlayer(string PlayerId, string PlayerName, int Score, bool IsHost);
public sealed record WebBoardTile(int Row, int Column, char Letter);
public sealed record WebPlayResponse(string GameId, string MoveId, int Score);

public sealed record WebPlayerProfile(string PlayerId, string Username, string? Email, DateTimeOffset CreatedAt);

public sealed record WebGameHistoryItem(
    string GameId,
    string GameLevel,
    string Queue,
    string Status,
    DateTimeOffset EndedAt,
    int DurationSeconds,
    bool IsWinner);


