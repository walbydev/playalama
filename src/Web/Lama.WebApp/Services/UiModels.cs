using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Lama.WebApp.Services;

public sealed class CreateGameForm
{
    [Required]
    public string HostName { get; set; } = "JoueurWeb";

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

public sealed class ProfileForm
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
}

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
    int CurrentPlayerIndex,
    int TurnNumber,
    IReadOnlyList<WebSnapshotPlayer> Players,
    IReadOnlyList<WebBoardTile> Board);

public sealed record WebSnapshotPlayer(string PlayerId, string PlayerName, int Score);
public sealed record WebBoardTile(int Row, int Column, char Letter);
public sealed record WebPlayResponse(string GameId, string MoveId, int Score);

