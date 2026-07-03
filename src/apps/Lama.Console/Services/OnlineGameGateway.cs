using System.Net.Http.Json;
using System.Text.Json;
using Lama.Contracts;
using Microsoft.Extensions.Logging;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Lama.Console.Services;

/// <summary>
/// Client HTTP minimal vers Lama.Server pour les opérations de partie online.
/// </summary>
public sealed class OnlineGameGateway
{
    private const string ApiBase = "/api/v1";
    private const string OnlineUsernameEnvVar = "LAMA_ONLINE_USERNAME";
    private const string OnlinePasswordEnvVar = "LAMA_ONLINE_PASSWORD";
    private const string DefaultOnlineUsername = "root";
    private const string DefaultOnlinePassword = "root";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly RuntimeModeService _runtimeMode;
    private readonly ILogger<OnlineGameGateway> _logger;
    private string? _authToken;

    public OnlineGameGateway(
        HttpClient httpClient,
        RuntimeModeService runtimeMode,
        ILogger<OnlineGameGateway> logger)
    {
        _httpClient = httpClient;
        _runtimeMode = runtimeMode;
        _logger = logger;
    }

    /// <summary>
    /// Authentifie un joueur et stocke le token JWT.
    /// </summary>
    public async Task<OnlineLoginResponse> LoginAsync(
        string playerName,
        string? playerId,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var payload = await LoginInternalAsync(playerName, playerId, cancellationToken);
        if (payload == null)
            throw new InvalidOperationException("Réponse serveur invalide sur auth.login.");

        _authToken = payload.Token;
        return payload;
    }

    /// <summary>
    /// Garantit qu'un token est disponible pour les endpoints protégés.
    /// </summary>
    public async Task<OnlineLoginResponse?> EnsureAuthenticatedAsync(
        string playerName,
        string? playerId,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        if (!string.IsNullOrWhiteSpace(_authToken))
            return null;

        var payload = await LoginInternalAsync(playerName, playerId, cancellationToken);
        if (payload == null)
            throw new InvalidOperationException("Réponse serveur invalide sur auth.login.");

        _authToken = payload.Token;
        return payload;
    }

    /// <summary>
    /// Définit manuellement le token (utile si récupéré depuis la session).
    /// </summary>
    public void SetAuthToken(string? token)
    {
        _authToken = token;
    }

    /// <summary>
    /// Retourne le token actuel.
    /// </summary>
    public string? GetAuthToken() => _authToken;

    private void SetAuthorizationHeader()
    {
        if (!string.IsNullOrEmpty(_authToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    public async Task<OnlineCreateGameResponse> CreateGameAsync(
        string hostName,
        GameLevel gameLevel,
        string language,
        string? mode,
        string? gameName,
        bool isPrivate,
        string? password,
        int? maxPlayers,
        bool enableAi,
        int? timePerPlayerSeconds,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();
        SetAuthorizationHeader();

        int? modeValue = null;
        if (!string.IsNullOrWhiteSpace(mode))
            modeValue = mode.Equals("multi", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        var request = new
        {
            hostName,
            gameLevel,
            language,
            mode = modeValue,
            gameName,
            isPrivate,
            password,
            maxPlayers,
            enableAi,
            timePerPlayerSeconds
        };

        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/games", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.create", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineCreateGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.create.");
    }

    public async Task<OnlineJoinGameResponse> JoinGameAsync(
        string gameId,
        string playerName,
        string? password,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();
        SetAuthorizationHeader();

        var request = new { playerName, password };
        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/games/{gameId}/join", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.join", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineJoinGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.join.");
    }

    public async Task<OnlineGameSnapshot> GetGameAsync(string gameId, CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var response = await _httpClient.GetAsync($"{ApiBase}/games/{gameId}", cancellationToken);
        await EnsureSuccessAsync(response, "game.show", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineGameSnapshot>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.show.");
    }

    public async Task<OnlineGameListResponse> ListGamesAsync(CancellationToken cancellationToken)
    {
        EnsureOnlineMode();

        var response = await _httpClient.GetAsync($"{ApiBase}/games", cancellationToken);
        await EnsureSuccessAsync(response, "game.list", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineGameListResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.list.");
    }

    public async Task<OnlinePlayCommandResponse> PlayCommandAsync(
        string gameId,
        string playerId,
        string command,
        object? payload,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();
        SetAuthorizationHeader();

        var request = new
        {
            playerId,
            command,
            payload
        };

        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/games/{gameId}/moves", request, cancellationToken);
        await EnsureSuccessAsync(response, "play.command", cancellationToken);

        var model = await response.Content.ReadFromJsonAsync<OnlinePlayCommandResponse>(JsonOptions, cancellationToken);
        return model ?? throw new InvalidOperationException("Réponse serveur invalide sur play.command.");
    }

    public async Task<OnlineEndGameResponse> EndGameAsync(
        string gameId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();
        SetAuthorizationHeader();

        var request = new
        {
            playerId
        };

        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/games/{gameId}/end", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.end", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineEndGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.end.");
    }

    public async Task<OnlineStartGameResponse> StartGameAsync(
        string gameId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        EnsureOnlineMode();
        SetAuthorizationHeader();

        var request = new { playerId };
        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/games/{gameId}/start", request, cancellationToken);
        await EnsureSuccessAsync(response, "game.start", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<OnlineStartGameResponse>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Réponse serveur invalide sur game.start.");
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
        string? errorMessage = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("error", out var errorProperty))
                    errorMessage = errorProperty.GetString();
            }
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

        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(errorMessage)
                ? $"Echec API online {op}: {(int)response.StatusCode}"
                : $"{errorMessage} (API online {op}, {(int)response.StatusCode})");
    }

    private async Task<OnlineLoginResponse?> LoginInternalAsync(
        string playerName,
        string? playerId,
        CancellationToken cancellationToken)
    {
        _ = playerId;

        var (username, password, explicitCredentials) = ResolveCredentials(playerName);

        var request = new { username, password };
        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/auth/login/account", request, cancellationToken);

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<OnlineLoginResponse>(JsonOptions, cancellationToken);

        if (!explicitCredentials && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var registerResponse = await _httpClient.PostAsJsonAsync(
                $"{ApiBase}/auth/register",
                new { username, password, email = (string?)null, countryCode = (string?)null },
                cancellationToken);

            if (registerResponse.StatusCode == HttpStatusCode.Conflict)
            {
                var retry = await _httpClient.PostAsJsonAsync($"{ApiBase}/auth/login/account", request, cancellationToken);
                await EnsureSuccessAsync(retry, "auth.login.account.retry", cancellationToken);
                return await retry.Content.ReadFromJsonAsync<OnlineLoginResponse>(JsonOptions, cancellationToken);
            }

            await EnsureSuccessAsync(registerResponse, "auth.register", cancellationToken);
            return await registerResponse.Content.ReadFromJsonAsync<OnlineLoginResponse>(JsonOptions, cancellationToken);
        }

        await EnsureSuccessAsync(response, "auth.login.account", cancellationToken);
        return await response.Content.ReadFromJsonAsync<OnlineLoginResponse>(JsonOptions, cancellationToken);
    }

    private static (string Username, string Password, bool ExplicitCredentials) ResolveCredentials(string playerName)
    {
        var envUsername = Environment.GetEnvironmentVariable(OnlineUsernameEnvVar);
        var envPassword = Environment.GetEnvironmentVariable(OnlinePasswordEnvVar);
        if (!string.IsNullOrWhiteSpace(envUsername) && !string.IsNullOrWhiteSpace(envPassword))
            return (envUsername.Trim(), envPassword, true);

        if (!string.IsNullOrWhiteSpace(playerName))
        {
            var normalized = playerName.Trim();
            var generatedPassword = $"lama-{normalized}-passwd";
            return (normalized, generatedPassword, false);
        }

        return (DefaultOnlineUsername, DefaultOnlinePassword, false);
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
    List<char> Rack,
    DateTimeOffset CreatedAt);

public sealed record OnlineJoinGameResponse(
    string GameId,
    string PlayerId,
    int Players,
    GameLevel GameLevel,
    RankingQueue Queue,
    List<char> Rack);

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
    DateTimeOffset UpdatedAt,
    bool IsGameOver,
    int CurrentPlayerIndex,
    int TurnNumber,
    List<OnlineSnapshotPlayer> Players,
    List<OnlineBoardTile> Board,
    List<OnlineSnapshotMove> Moves);

public sealed record OnlineSnapshotPlayer(
    string PlayerId,
    string PlayerName,
    bool IsHost,
    int Score,
    List<char> Rack,
    int RackCount);

public sealed record OnlineBoardTile(int Row, int Column, char Letter, bool IsWildcard);

public sealed record OnlineSnapshotMove(
    string MoveId,
    string PlayerId,
    string PlayerName,
    string Command,
    JsonElement? Payload,
    DateTimeOffset PlayedAt,
    int Score,
    int TurnNumber = 0,
    List<OnlineSnapshotMovePlacement>? Placements = null);

public sealed record OnlineSnapshotMovePlacement(int Row, int Column, char Letter);

public sealed record OnlinePlayCommandResponse(
    string GameId,
    string MoveId,
    DateTimeOffset PlayedAt,
    int Score,
    List<char>? NewRack,
    int CurrentPlayerIndex,
    string? NextPlayerId,
    string? Message = null,
    bool? ChallengeSucceeded = null,
    List<OnlineSuggestedMove>? Suggestions = null);

public sealed record OnlineSuggestedMove(
    string Word,
    string Position,
    string Direction,
    int Score,
    int Length,
    double BalancedScore = 0);

public sealed record OnlineGameListResponse(
    int Total,
    List<OnlineGameListItem> Games);

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

public sealed record OnlineEndGameResponse(
    string GameId,
    bool IsGameOver,
    string? Winner,
    List<OnlineScoreEntry> Scores,
    DateTimeOffset EndedAt);

public sealed record OnlineStartGameResponse(
    string GameId,
    bool HasStarted,
    int MaxPlayers,
    int ReservedAiSlots);

public sealed record OnlineScoreEntry(string PlayerName, int Score);

public sealed record OnlineLoginResponse(
    string Token,
    string PlayerId,
    string PlayerName,
    DateTime ExpiresAt);
