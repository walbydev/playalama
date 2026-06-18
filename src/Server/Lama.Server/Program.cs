using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Lama.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GameHubState>();

var app = builder.Build();

var allowShutdown = string.Equals(
    Environment.GetEnvironmentVariable("LAMA_SERVER_ALLOW_SHUTDOWN"),
    "true",
    StringComparison.OrdinalIgnoreCase);

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    utcNow = DateTimeOffset.UtcNow
}));

app.MapPost("/internal/shutdown", (IHostApplicationLifetime lifetime) =>
{
    if (!allowShutdown)
        return Results.NotFound();

    lifetime.StopApplication();
    return Results.Ok(new
    {
        status = "stopping",
        utcNow = DateTimeOffset.UtcNow
    });
});

app.MapPost("/api/games", (CreateGameRequest request, GameHubState state) =>
{
    if (string.IsNullOrWhiteSpace(request.HostName))
        return Results.BadRequest(new { error = "hostName is required" });

    var gameId = Guid.NewGuid().ToString("N");
    var hostId = Guid.NewGuid().ToString("N");

    var game = new OnlineGame(
        Id: gameId,
        GameLevel: request.GameLevel ?? GameLevel.Standard,
        BoardSize: request.BoardSize > 0 ? request.BoardSize : 15,
        RackSize: request.RackSize > 0 ? request.RackSize : 7,
        MinWordLength: request.MinWordLength > 0 ? request.MinWordLength : 2,
        Language: string.IsNullOrWhiteSpace(request.Language) ? "fr" : request.Language.Trim(),
        CreatedAt: DateTimeOffset.UtcNow,
        Players:
        [
            new OnlinePlayer(hostId, request.HostName.Trim(), true)
        ],
        Moves: [],
        CurrentPlayerIndex: 0,
        IsGameOver: false,
        TournamentId: request.TournamentId,
        Queue: ResolveQueue(request.GameLevel ?? GameLevel.Standard));

    state.Create(game);

    state.Publish(gameId, new ServerEvent("game.created", new
    {
        gameId,
        hostPlayerId = hostId,
        game.GameLevel,
        game.Queue,
        game.BoardSize,
        game.RackSize,
        game.MinWordLength,
        game.Language
    }));

    return Results.Ok(new
    {
        gameId,
        hostPlayerId = hostId,
        game.GameLevel,
        game.Queue,
        game.BoardSize,
        game.RackSize,
        game.MinWordLength,
        game.Language,
        game.CreatedAt
    });
});

app.MapPost("/api/games/{gameId}/join", (string gameId, JoinGameRequest request, GameHubState state) =>
{
    if (string.IsNullOrWhiteSpace(request.PlayerName))
        return Results.BadRequest(new { error = "playerName is required" });

    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    var playerId = Guid.NewGuid().ToString("N");
    lock (game)
    {
        if (game.IsGameOver)
            return Results.BadRequest(new { error = "game is over" });

        game.Players.Add(new OnlinePlayer(playerId, request.PlayerName.Trim(), false));
    }

    state.Publish(gameId, new ServerEvent("game.joined", new
    {
        gameId,
        playerId,
        playerName = request.PlayerName.Trim(),
        players = game.Players.Count
    }));

    return Results.Ok(new
    {
        gameId,
        playerId,
        players = game.Players.Count,
        game.GameLevel,
        game.Queue
    });
});

app.MapPost("/api/games/{gameId}/moves", (string gameId, PlayMoveRequest request, GameHubState state) =>
{
    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    if (string.IsNullOrWhiteSpace(request.PlayerId))
        return Results.BadRequest(new { error = "playerId is required" });

    if (string.IsNullOrWhiteSpace(request.Command))
        return Results.BadRequest(new { error = "command is required" });

    OnlineMove createdMove;

    lock (game)
    {
        if (game.IsGameOver)
            return Results.BadRequest(new { error = "game is over" });

        var currentPlayer = game.Players.ElementAtOrDefault(game.CurrentPlayerIndex);
        if (currentPlayer is null)
            return Results.BadRequest(new { error = "no active player" });

        if (!string.Equals(currentPlayer.PlayerId, request.PlayerId, StringComparison.Ordinal))
            return Results.BadRequest(new
            {
                error = "not your turn",
                expectedPlayerId = currentPlayer.PlayerId,
                currentPlayerName = currentPlayer.PlayerName
            });

        createdMove = new OnlineMove(
            MoveId: Guid.NewGuid().ToString("N"),
            PlayerId: request.PlayerId,
            Command: request.Command.Trim(),
            Payload: request.Payload,
            PlayedAt: DateTimeOffset.UtcNow);

        game.Moves.Add(createdMove);

        if (game.Players.Count > 0)
            game.CurrentPlayerIndex = (game.CurrentPlayerIndex + 1) % game.Players.Count;
    }

    state.Publish(gameId, new ServerEvent("game.move.played", new
    {
        gameId,
        createdMove.MoveId,
        createdMove.PlayerId,
        createdMove.Command,
        createdMove.Payload,
        createdMove.PlayedAt,
        game.CurrentPlayerIndex,
        nextPlayerId = game.Players.ElementAtOrDefault(game.CurrentPlayerIndex)?.PlayerId
    }));

    return Results.Ok(new
    {
        gameId,
        createdMove.MoveId,
        createdMove.PlayedAt,
        game.CurrentPlayerIndex,
        nextPlayerId = game.Players.ElementAtOrDefault(game.CurrentPlayerIndex)?.PlayerId
    });
});

app.MapGet("/api/games/{gameId}", (string gameId, GameHubState state) =>
{
    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    return Results.Ok(new
    {
        game.Id,
        game.GameLevel,
        game.Queue,
        game.BoardSize,
        game.RackSize,
        game.MinWordLength,
        game.Language,
        game.TournamentId,
        game.CreatedAt,
        game.IsGameOver,
        game.CurrentPlayerIndex,
        players = game.Players,
        moves = game.Moves
    });
});

app.MapPost("/api/games/{gameId}/end", (string gameId, EndGameRequest request, GameHubState state) =>
{
    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    List<OnlineScoreEntry> scores;

    lock (game)
    {
        if (game.IsGameOver)
            return Results.BadRequest(new { error = "game is already over" });

        game.IsGameOver = true;
        scores = game.Players
            .Select(p => new OnlineScoreEntry(p.PlayerName, 0))
            .ToList();
    }

    var endedAt = DateTimeOffset.UtcNow;

    state.Publish(gameId, new ServerEvent("game.ended", new
    {
        gameId,
        endedAt,
        request.PlayerId,
        scores,
        winner = (string?)null
    }));

    return Results.Ok(new
    {
        gameId,
        isGameOver = true,
        winner = (string?)null,
        scores,
        endedAt
    });
});

app.MapGet("/api/games/{gameId}/events", async (string gameId, GameHubState state, HttpContext httpContext) =>
{
    if (!state.Exists(gameId))
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        await httpContext.Response.WriteAsJsonAsync(new { error = "game not found" });
        return;
    }

    httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
    httpContext.Response.Headers.Append("Cache-Control", "no-cache");

    var subscription = state.Subscribe(gameId);

    // Confirmation initiale de connexion SSE
    await WriteEventAsync(httpContext, new ServerEvent("sse.connected", new
    {
        gameId,
        utcNow = DateTimeOffset.UtcNow
    }));

    try
    {
        while (!httpContext.RequestAborted.IsCancellationRequested)
        {
            while (subscription.Reader.TryRead(out var evt))
                await WriteEventAsync(httpContext, evt);

            await Task.Delay(150, httpContext.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // fermeture normale du flux
    }
    finally
    {
        state.Unsubscribe(gameId, subscription.Id);
    }
});

app.Run();

static RankingQueue ResolveQueue(GameLevel level) => level switch
{
    GameLevel.Casual => RankingQueue.CasualUnranked,
    GameLevel.Tournament => RankingQueue.Tournament,
    _ => RankingQueue.OpenRanked
};

static async Task WriteEventAsync(HttpContext context, ServerEvent evt)
{
    var payloadJson = JsonSerializer.Serialize(evt.Payload);
    await context.Response.WriteAsync($"event: {evt.Type}\n");
    await context.Response.WriteAsync($"data: {payloadJson}\n\n");
    await context.Response.Body.FlushAsync();
}

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
    List<OnlinePlayer> Players,
    List<OnlineMove> Moves,
    int CurrentPlayerIndex,
    bool IsGameOver,
    string? TournamentId,
    RankingQueue Queue)
{
    public string Id { get; } = Id;
    public GameLevel GameLevel { get; } = GameLevel;
    public int BoardSize { get; } = BoardSize;
    public int RackSize { get; } = RackSize;
    public int MinWordLength { get; } = MinWordLength;
    public string Language { get; } = Language;
    public DateTimeOffset CreatedAt { get; } = CreatedAt;
    public List<OnlinePlayer> Players { get; } = Players;
    public List<OnlineMove> Moves { get; } = Moves;
    public int CurrentPlayerIndex { get; set; } = CurrentPlayerIndex;
    public bool IsGameOver { get; set; } = IsGameOver;
    public string? TournamentId { get; } = TournamentId;
    public RankingQueue Queue { get; } = Queue;
}

public sealed record OnlinePlayer(string PlayerId, string PlayerName, bool IsHost);

public sealed record OnlineMove(
    string MoveId,
    string PlayerId,
    string Command,
    JsonElement? Payload,
    DateTimeOffset PlayedAt);

public sealed record OnlineScoreEntry(string PlayerName, int Score);

public sealed record ServerEvent(string Type, object Payload);

public sealed class GameHubState
{
    private readonly ConcurrentDictionary<string, OnlineGame> _games = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EventSubscribers> _subscribers = new(StringComparer.Ordinal);

    public void Create(OnlineGame game)
    {
        _games[game.Id] = game;
        _subscribers.TryAdd(game.Id, new EventSubscribers());
    }

    public bool Exists(string gameId) => _games.ContainsKey(gameId);

    public bool TryGet(string gameId, out OnlineGame game) => _games.TryGetValue(gameId, out game!);

    public SubscriberToken Subscribe(string gameId)
    {
        var subscribers = _subscribers.GetOrAdd(gameId, _ => new EventSubscribers());
        return subscribers.Add();
    }

    public void Unsubscribe(string gameId, string subscriberId)
    {
        if (_subscribers.TryGetValue(gameId, out var subscribers))
            subscribers.Remove(subscriberId);
    }

    public void Publish(string gameId, ServerEvent evt)
    {
        if (_subscribers.TryGetValue(gameId, out var subscribers))
            subscribers.Broadcast(evt);
    }

    private sealed class EventSubscribers
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, Channel<ServerEvent>> _channels = new(StringComparer.Ordinal);

        public SubscriberToken Add()
        {
            var id = Guid.NewGuid().ToString("N");
            var channel = Channel.CreateUnbounded<ServerEvent>();

            lock (_sync)
            {
                _channels[id] = channel;
            }

            return new SubscriberToken(id, channel.Reader);
        }

        public void Remove(string id)
        {
            Channel<ServerEvent>? channel = null;
            lock (_sync)
            {
                if (_channels.TryGetValue(id, out channel))
                    _channels.Remove(id);
            }

            channel?.Writer.TryComplete();
        }

        public void Broadcast(ServerEvent evt)
        {
            lock (_sync)
            {
                foreach (var channel in _channels.Values)
                    channel.Writer.TryWrite(evt);
            }
        }
    }
}

public sealed record SubscriberToken(string Id, ChannelReader<ServerEvent> Reader);


