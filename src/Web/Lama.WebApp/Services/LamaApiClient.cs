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

    public async Task<IReadOnlyList<WebGameListItem>> ListGamesAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{ApiBase}/games", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GameListEnvelope>(JsonOptions, cancellationToken);
        return payload?.Games
            ?.Select(x => new WebGameListItem(
                x.Id,
                x.GameName,
                x.Status,
                x.Players,
                x.MaxPlayers,
                x.Queue,
                x.IsJoinable))
            .ToList() ?? [];
    }

    public async Task<WebCreateGameResponse> CreateGameAsync(CreateGameForm form, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            hostName = form.HostName,
            gameLevel = GameLevel.Standard,
            mode = string.Equals(form.Mode, "multi", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            gameName = form.GameName,
            maxPlayers = form.MaxPlayers,
            enableAi = false,
            language = "fr"
        };

        var response = await httpClient.PostAsJsonAsync($"{ApiBase}/games", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<CreateGameEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Reponse invalide sur game.create.");

        return new WebCreateGameResponse(payload.GameId, payload.HostPlayerId);
    }

    public async Task<WebJoinGameResponse> JoinGameAsync(string gameId, string playerName, string? password, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{ApiBase}/games/{gameId}/join", new { playerName, password }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<JoinGameEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Reponse invalide sur game.join.");

        return new WebJoinGameResponse(payload.GameId, payload.PlayerId);
    }

    public async Task<WebGameSnapshot> GetGameAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{ApiBase}/games/{gameId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GameSnapshotEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Reponse invalide sur game.show.");

        return new WebGameSnapshot(
            payload.Id,
            payload.IsGameOver,
            payload.CurrentPlayerIndex,
            payload.TurnNumber,
            payload.Players.Select(x => new WebSnapshotPlayer(x.PlayerId, x.PlayerName, x.Score)).ToList(),
            payload.Board.Select(x => new WebBoardTile(x.Row, x.Column, x.Letter)).ToList());
    }

    public async Task<WebPlayResponse> PlayAsync(string gameId, PlayForm form, CancellationToken cancellationToken = default)
    {
        object? payload = form.Command switch
        {
            "play.move" => new { position = form.Position, word = form.Word, direction = form.Direction },
            "play.swap" => new { letters = form.Word },
            _ => null
        };

        var request = new
        {
            playerId = form.PlayerId,
            command = form.Command,
            payload
        };

        var response = await httpClient.PostAsJsonAsync($"{ApiBase}/games/{gameId}/moves", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<PlayEnvelope>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Reponse invalide sur play.command.");

        return new WebPlayResponse(result.GameId, result.MoveId, result.Score);
    }

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
                ? error.GetString() ?? "Erreur API"
                : $"Erreur API HTTP {(int)response.StatusCode}";
        }
        catch
        {
            message = string.IsNullOrWhiteSpace(body) ? $"Erreur API HTTP {(int)response.StatusCode}" : body;
        }

        throw new HttpRequestException(message);
    }

    private sealed record GameListEnvelope(List<GameListItemEnvelope> Games);

    private sealed record GameListItemEnvelope(
        string Id,
        string? GameName,
        string Status,
        int Players,
        int MaxPlayers,
        string Queue,
        bool IsJoinable);

    private sealed record CreateGameEnvelope(string GameId, string HostPlayerId);
    private sealed record JoinGameEnvelope(string GameId, string PlayerId);

    private sealed record GameSnapshotEnvelope(
        string Id,
        bool IsGameOver,
        int CurrentPlayerIndex,
        int TurnNumber,
        List<GameSnapshotPlayerEnvelope> Players,
        List<GameBoardTileEnvelope> Board);

    private sealed record GameSnapshotPlayerEnvelope(string PlayerId, string PlayerName, int Score);
    private sealed record GameBoardTileEnvelope(int Row, int Column, char Letter);
    private sealed record PlayEnvelope(string GameId, string MoveId, int Score);
}

