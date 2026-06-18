using System.Net.Http.Json;
using System.Text.Json;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Services;

/// <summary>
/// Client HTTP minimal vers Lama.Server pour les opérations de partie online.
/// </summary>
public sealed class OnlineGameGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly RuntimeModeService _runtimeMode;
    private readonly ILogger<OnlineGameGateway> _logger;

    public OnlineGameGateway(
        HttpClient httpClient,
        RuntimeModeService runtimeMode,
        ILogger<OnlineGameGateway> logger)
    {
        _httpClient = httpClient;
        _runtimeMode = runtimeMode;
        _logger = logger;
    }

    public async Task<OnlineCreateGameResponse> CreateGameAsync(
        string hostName,
        GameLevel gameLevel,
        string language,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var request = new
        {
            hostName,
            gameLevel,
            language
        };

        var response = await _httpClient.PostAsJsonAsync("/api/games", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.create", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineCreateGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.create.");
    }

    public async Task<OnlineJoinGameResponse> JoinGameAsync(
        string gameId,
        string playerName,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var request = new { playerName };
        var response = await _httpClient.PostAsJsonAsync($"/api/games/{gameId}/join", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.join", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineJoinGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.join.");
    }

    public async Task<OnlineGameSnapshot> GetGameAsync(string gameId, CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var response = await _httpClient.GetAsync($"/api/games/{gameId}", cancellationToken);
        await EnsureSuccessAsync(response, "game.show", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineGameSnapshot>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.show.");
    }

    public async Task<OnlineEndGameResponse> EndGameAsync(
        string gameId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var request = new
        {
            playerId
        };

        var response = await _httpClient.PostAsJsonAsync($"/api/games/{gameId}/end", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.end", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineEndGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.end.");
    }

    private void EnsureOnlineMode()
    {
        if (!_runtimeMode.IsOnline)
            throw new InvalidOperationException("OnlineGameGateway appelé alors que LAMA_RUNTIME_MODE != online.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string op, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // best effort
        }

        _logger.LogWarning(
            "Echec API online {Operation}: {StatusCode} {Body}",
            op,
            (int)response.StatusCode,
            body ?? "(empty)");

        throw new HttpRequestException($"Echec API online {op}: {(int)response.StatusCode}");
    }
}

public sealed record OnlineCreateGameResponse(
    string GameId,
    string HostPlayerId,
    GameLevel GameLevel,
    RankingQueue Queue,
    int BoardSize,
    int RackSize,
    int MinWordLength,
    string Language,
    DateTimeOffset CreatedAt);

public sealed record OnlineJoinGameResponse(
    string GameId,
    string PlayerId,
    int Players,
    GameLevel GameLevel,
    RankingQueue Queue);

public sealed record OnlineGameSnapshot(
    string Id,
    GameLevel GameLevel,
    RankingQueue Queue,
    int BoardSize,
    int RackSize,
    int MinWordLength,
    string Language,
    string? TournamentId,
    DateTimeOffset CreatedAt,
    bool IsGameOver,
    int CurrentPlayerIndex,
    List<OnlineSnapshotPlayer> Players,
    List<OnlineSnapshotMove> Moves);

public sealed record OnlineSnapshotPlayer(string PlayerId, string PlayerName, bool IsHost);

public sealed record OnlineSnapshotMove(
    string MoveId,
    string PlayerId,
    string Command,
    JsonElement? Payload,
    DateTimeOffset PlayedAt);

public sealed record OnlineEndGameResponse(
    string GameId,
    bool IsGameOver,
    string? Winner,
    List<OnlineScoreEntry> Scores,
    DateTimeOffset EndedAt);

public sealed record OnlineScoreEntry(string PlayerName, int Score);

