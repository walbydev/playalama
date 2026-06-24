using System.Net.Http.Json;
using System.Text.Json;
using Lama.Contracts;

namespace Lama.WebApp.Services;

public sealed class LamaApiClient(HttpClient httpClient)
{
    private const string ApiBase = "/api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Auth ─────────────────────────────────────────────────────────────────

    public async Task<WebAuthResponse> RegisterAsync(string username, string password, string? email, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{ApiBase}/auth/register", new { username, password, email }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse invalide sur auth/register.");

        return new WebAuthResponse(payload.Token, payload.PlayerId, payload.PlayerName, payload.Email, payload.ExpiresAt);
    }

    public async Task<WebAuthResponse> AccountLoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{ApiBase}/auth/login/account", new { username, password }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse invalide sur auth/login/account.");

        return new WebAuthResponse(payload.Token, payload.PlayerId, payload.PlayerName, payload.Email, payload.ExpiresAt);
    }

    // ── Profil joueur ────────────────────────────────────────────────────────

    public async Task<WebPlayerProfile?> GetMyProfileAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/players/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<PlayerProfileEnvelope>(JsonOptions, cancellationToken);
        return payload is null ? null : new WebPlayerProfile(payload.PlayerId, payload.Username, payload.Email, payload.CreatedAt);
    }

    public async Task<WebPlayerProfile?> UpdateMyProfileAsync(string token, string? email, string? currentPassword, string? newPassword, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiBase}/players/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { email, currentPassword, newPassword });

        var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<PlayerProfileEnvelope>(JsonOptions, cancellationToken);
        return payload is null ? null : new WebPlayerProfile(payload.PlayerId, payload.Username, payload.Email, payload.CreatedAt);
    }

    public async Task<IReadOnlyList<WebGameHistoryItem>> GetMyGamesAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/players/me/games");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [];

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<List<GameHistoryEnvelope>>(JsonOptions, cancellationToken);
        return payload?.Select(x => new WebGameHistoryItem(x.GameId, x.GameLevel, x.Queue, x.Status, x.EndedAt, x.DurationSeconds, x.IsWinner)).ToList() ?? [];
    }

    // ── Classements ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        string mode = "tournament",
        string? countryCode = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ApiBase}/leaderboard?mode={Uri.EscapeDataString(mode)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(countryCode))
            url += $"&country={Uri.EscapeDataString(countryCode)}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        var payload = await response.Content.ReadFromJsonAsync<List<LeaderboardEntryEnvelope>>(JsonOptions, cancellationToken);
        return payload?
            .Select((x, i) => new LeaderboardEntry(i + 1, x.PlayerId, x.Username, x.CountryCode, x.Level, x.Elo, x.Wins, x.Games))
            .ToList() ?? [];
    }

    // ── Parties ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WebGameListItem>> ListGamesAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{ApiBase}/games", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GameListEnvelope>(JsonOptions, cancellationToken);
        return payload?.Games?.Select(x => new WebGameListItem(x.Id, x.GameName, x.Status, x.Players, x.MaxPlayers, NormalizeQueue(x.Queue), x.IsJoinable)).ToList() ?? [];
    }

    public async Task<WebCreateGameResponse> CreateGameAsync(CreateGameForm form, string hostName, string? token = null, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            hostName,
            gameLevel = GameLevel.Standard,
            mode = string.Equals(form.Mode, "multi", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            gameName = form.GameName,
            maxPlayers = form.MaxPlayers,
            enableAi = false,
            language = "fr"
        };

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{ApiBase}/games", token);
        httpRequest.Content = JsonContent.Create(request);
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<CreateGameEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse invalide sur game.create.");

        return new WebCreateGameResponse(payload.GameId, payload.HostPlayerId);
    }

    public async Task<WebJoinGameResponse> JoinGameAsync(string gameId, string playerName, string? password, string? token = null, CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{ApiBase}/games/{gameId}/join", token);
        httpRequest.Content = JsonContent.Create(new { playerName, password });
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<JoinGameEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse invalide sur game.join.");

        return new WebJoinGameResponse(payload.GameId, payload.PlayerId);
    }

    public async Task StartGameAsync(string gameId, string? token = null, CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{ApiBase}/games/{gameId}/start", token);
        httpRequest.Content = JsonContent.Create(new { playerId = (string?)null });
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<WebGameSnapshot> GetGameAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{ApiBase}/games/{gameId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GameSnapshotEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse invalide sur game.show.");

        return new WebGameSnapshot(
            payload.Id,
            payload.IsGameOver,
            payload.HasStarted,
            payload.UsesLobby,
            payload.CurrentPlayerIndex,
            payload.TurnNumber,
            payload.Players.Select(x => new WebSnapshotPlayer(x.PlayerId, x.PlayerName, x.Score, x.IsHost)).ToList(),
            payload.Board.Select(x => new WebBoardTile(x.Row, x.Column, x.Letter)).ToList());
    }

    public async Task<WebPlayResponse> PlayAsync(string gameId, PlayForm form, string? token = null, CancellationToken cancellationToken = default)
    {
        object? payload = form.Command switch
        {
            "play.move" => new { position = form.Position, word = form.Word, direction = form.Direction },
            "play.swap" => new { letters = form.Word },
            _ => null
        };

        var request = new { playerId = form.PlayerId, command = form.Command, payload };
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{ApiBase}/games/{gameId}/moves", token);
        httpRequest.Content = JsonContent.Create(request);
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<PlayEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse invalide sur play.command.");

        return new WebPlayResponse(result.GameId, result.MoveId, result.Score);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message;

        try
        {
            using var document = JsonDocument.Parse(body);
            message = document.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? $"Erreur API HTTP {(int)response.StatusCode}"
                : $"Erreur API HTTP {(int)response.StatusCode}";
        }
        catch
        {
            message = string.IsNullOrWhiteSpace(body)
                ? $"Erreur API HTTP {(int)response.StatusCode}"
                : body;
        }

        throw new HttpRequestException(message);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static string NormalizeQueue(JsonElement queue)
    {
        return queue.ValueKind switch
        {
            JsonValueKind.String => queue.GetString() ?? "unknown",
            JsonValueKind.Number when queue.TryGetInt32(out var value) => value switch
            {
                0 => "Casual",
                1 => "Ranked",
                _ => value.ToString()
            },
            _ => queue.ToString()
        };
    }

    // ── Enveloppes API (privées) ──────────────────────────────────────────────

    private sealed record AuthEnvelope(string Token, string PlayerId, string PlayerName, string? Email, DateTime ExpiresAt);
    private sealed record PlayerProfileEnvelope(string PlayerId, string Username, string? Email, DateTimeOffset CreatedAt);
    private sealed record GameHistoryEnvelope(string GameId, string GameLevel, string Queue, string Status, DateTimeOffset EndedAt, int DurationSeconds, bool IsWinner);
    private sealed record GameListEnvelope(List<GameListItemEnvelope> Games);
    private sealed record GameListItemEnvelope(string Id, string? GameName, string Status, int Players, int MaxPlayers, JsonElement Queue, bool IsJoinable);
    private sealed record CreateGameEnvelope(string GameId, string HostPlayerId);
    private sealed record JoinGameEnvelope(string GameId, string PlayerId);
    private sealed record GameSnapshotEnvelope(string Id, bool IsGameOver, bool HasStarted, bool UsesLobby, int CurrentPlayerIndex, int TurnNumber, List<GameSnapshotPlayerEnvelope> Players, List<GameBoardTileEnvelope> Board);
    private sealed record GameSnapshotPlayerEnvelope(string PlayerId, string PlayerName, int Score, bool IsHost);
    private sealed record GameBoardTileEnvelope(int Row, int Column, char Letter);
    private sealed record PlayEnvelope(string GameId, string MoveId, int Score);
    private sealed record LeaderboardEntryEnvelope(string PlayerId, string Username, string? CountryCode, int Level, int Elo, int Wins, int Games);
}
