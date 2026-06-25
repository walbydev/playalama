using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

/// <summary>
/// Encapsule l'état de la salle d'attente (lobby) pour une partie multijoueur.
/// Instancié directement dans Lobby.razor (pas via DI) pour isoler l'état par page.
/// </summary>
public sealed class LobbyViewModel
{
    private string? _myPlayerId;

    public string GameId { get; private set; } = string.Empty;
    public string? GameName { get; private set; }
    public int MaxPlayers { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public WebGameSnapshot? Snapshot { get; private set; }

    public bool IsHost => Snapshot?.Players.Any(p => p.IsHost
        && string.Equals(p.PlayerId, _myPlayerId, StringComparison.Ordinal)) ?? false;

    public bool IsParticipant => _myPlayerId is not null
        && (Snapshot?.Players.Any(p => string.Equals(p.PlayerId, _myPlayerId, StringComparison.Ordinal)) ?? false);

    public bool CanStart => Snapshot is { HasStarted: false, UsesLobby: true }
                            && IsHost
                            && Snapshot.Players.Count >= 2;

    public bool CanForceStart => Snapshot is { HasStarted: false, UsesLobby: true } && IsHost;

    public bool HasStarted => Snapshot?.HasStarted ?? false;

    public void Initialize(string gameId, string? myPlayerId)
    {
        GameId = gameId;
        _myPlayerId = myPlayerId;
    }

    public async Task LoadAsync(LamaApiClient api)
    {
        IsLoading = true;
        Error = null;
        try
        {
            Snapshot = await api.GetGameAsync(GameId);
            GameName = Snapshot.GameName;
            MaxPlayers = Snapshot.MaxPlayers;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task StartAsync(LamaApiClient api, string token)
    {
        IsLoading = true;
        Error = null;
        try { await api.StartGameAsync(GameId, token); }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task ForceStartAsync(LamaApiClient api, string token)
    {
        IsLoading = true;
        Error = null;
        try { await api.ForceStartGameAsync(GameId, token); }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
