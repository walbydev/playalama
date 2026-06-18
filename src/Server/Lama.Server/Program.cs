using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Lama.Contracts;
using Lama.Domain.Board;
using Lama.Domain.Engine;
using Lama.Languages.fr;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameLanguageProvider>(_ =>
{
    var basePath = Path.Combine(AppContext.BaseDirectory, "assets", "languages", "fr");
    return new FrenchLanguageProvider(basePath);
});
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
    var hostName = request.HostName.Trim();
    var level = request.GameLevel ?? GameLevel.Standard;

    var engine = state.CreateEngine();
    engine.InitializeGame([hostName]);
    var initialState = engine.GetGameState();

    var game = new OnlineGame(
        Id: gameId,
        GameLevel: level,
        BoardSize: request.BoardSize > 0 ? request.BoardSize : 15,
        RackSize: request.RackSize > 0 ? request.RackSize : 7,
        MinWordLength: request.MinWordLength > 0 ? request.MinWordLength : 2,
        Language: string.IsNullOrWhiteSpace(request.Language) ? "fr" : request.Language.Trim(),
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow,
        Players: [new OnlinePlayer(hostId, hostName, true)],
        PlayerIndexById: new Dictionary<string, int>(StringComparer.Ordinal) { [hostId] = 0 },
        Moves: [],
        TournamentId: request.TournamentId,
        Queue: ResolveQueue(level),
        Engine: engine);

    state.Create(game);

    state.Publish(gameId, new ServerEvent("game.created", new
    {
        gameId,
        hostPlayerId = hostId,
        level,
        queue = game.Queue,
        rack = initialState.Players[0].Rack,
        createdAt = game.CreatedAt
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
        rack = initialState.Players[0].Rack,
        game.CreatedAt
    });
});

app.MapPost("/api/games/{gameId}/join", (string gameId, JoinGameRequest request, GameHubState state) =>
{
    if (string.IsNullOrWhiteSpace(request.PlayerName))
        return Results.BadRequest(new { error = "playerName is required" });

    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    var trimmedName = request.PlayerName.Trim();
    var playerId = Guid.NewGuid().ToString("N");
    List<char> rack;

    lock (game)
    {
        var currentState = game.Engine.GetGameState();
        if (currentState.IsGameOver)
            return Results.BadRequest(new { error = "game is over" });

        if (currentState.History.Count > 0)
            return Results.BadRequest(new { error = "cannot join a game that has already started" });

        var allPlayerNames = game.Players.Select(p => p.PlayerName).ToList();
        allPlayerNames.Add(trimmedName);
        game.Engine.InitializeGame(allPlayerNames);

        game.Players.Add(new OnlinePlayer(playerId, trimmedName, false));
        game.PlayerIndexById[playerId] = game.Players.Count - 1;
        game.UpdatedAt = DateTimeOffset.UtcNow;

        var newState = game.Engine.GetGameState();
        rack = newState.Players[game.PlayerIndexById[playerId]].Rack.ToList();
    }

    state.Publish(gameId, new ServerEvent("game.joined", new
    {
        gameId,
        playerId,
        playerName = trimmedName,
        players = game.Players.Count
    }));

    return Results.Ok(new
    {
        gameId,
        playerId,
        players = game.Players.Count,
        game.GameLevel,
        game.Queue,
        rack
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

    var normalizedCommand = request.Command.Trim().ToLowerInvariant();
    OnlineMove createdMove;
    int score = 0;
    List<char>? newRack = null;
    int nextCurrentPlayerIndex;
    string? nextPlayerId;

    lock (game)
    {
        var currentState = game.Engine.GetGameState();
        if (currentState.IsGameOver)
            return Results.BadRequest(new { error = "game is over" });

        if (!game.PlayerIndexById.TryGetValue(request.PlayerId, out var playerIndex))
            return Results.BadRequest(new { error = "unknown playerId" });

        if (currentState.CurrentPlayerIndex != playerIndex)
        {
            var expected = game.Players.ElementAtOrDefault(currentState.CurrentPlayerIndex);
            return Results.BadRequest(new
            {
                error = "not your turn",
                expectedPlayerId = expected?.PlayerId,
                currentPlayerName = expected?.PlayerName
            });
        }

        try
        {
            switch (normalizedCommand)
            {
                case "play.pass":
                    game.Engine.PassTurn();
                    newRack = currentState.Players[playerIndex].Rack.ToList();
                    break;

                case "play.move":
                    var letters = BuildLetterPlacementsFromPayload(request.Payload);
                    var validation = game.Engine.ValidateMove(letters);
                    if (!validation.IsValid)
                        return Results.BadRequest(new { error = validation.ErrorMessage });

                    score = validation.Score;
                    var stateAfterMove = game.Engine.PlayMove(letters);
                    newRack = stateAfterMove.Players[playerIndex].Rack.ToList();
                    break;

                default:
                    return Results.BadRequest(new { error = $"unsupported command: {normalizedCommand}" });
            }
        }
        catch (GameException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        createdMove = new OnlineMove(
            MoveId: Guid.NewGuid().ToString("N"),
            PlayerId: request.PlayerId,
            PlayerName: game.Players[playerIndex].PlayerName,
            Command: normalizedCommand,
            Payload: request.Payload,
            PlayedAt: DateTimeOffset.UtcNow,
            Score: score);

        game.Moves.Add(createdMove);
        game.UpdatedAt = DateTimeOffset.UtcNow;

        var updatedState = game.Engine.GetGameState();
        nextCurrentPlayerIndex = updatedState.CurrentPlayerIndex;
        nextPlayerId = game.Players.ElementAtOrDefault(nextCurrentPlayerIndex)?.PlayerId;
    }

    state.Publish(gameId, new ServerEvent("game.move.played", new
    {
        gameId,
        createdMove.MoveId,
        createdMove.PlayerId,
        createdMove.PlayerName,
        createdMove.Command,
        createdMove.Score,
        createdMove.Payload,
        createdMove.PlayedAt,
        currentPlayerIndex = nextCurrentPlayerIndex,
        nextPlayerId
    }));

    return Results.Ok(new
    {
        gameId,
        createdMove.MoveId,
        createdMove.PlayedAt,
        score,
        newRack,
        currentPlayerIndex = nextCurrentPlayerIndex,
        nextPlayerId
    });
});

app.MapGet("/api/games/{gameId}", (string gameId, GameHubState state) =>
{
    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    lock (game)
    {
        var stateSnapshot = game.Engine.GetGameState();
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
            game.UpdatedAt,
            stateSnapshot.IsGameOver,
            stateSnapshot.CurrentPlayerIndex,
            stateSnapshot.TurnNumber,
            players = game.Players.Select((player, index) => new
            {
                player.PlayerId,
                player.PlayerName,
                player.IsHost,
                Score = stateSnapshot.Players[index].Score,
                Rack = stateSnapshot.Players[index].Rack,
                RackCount = stateSnapshot.Players[index].Rack.Count
            }),
            board = CaptureBoard(stateSnapshot.Board),
            moves = game.Moves
        });
    }
});

app.MapPost("/api/games/{gameId}/end", (string gameId, EndGameRequest request, GameHubState state) =>
{
    if (!state.TryGet(gameId, out var game))
        return Results.NotFound(new { error = "game not found" });

    string? winner;
    List<OnlineScoreEntry> scores;

    lock (game)
    {
        var currentState = game.Engine.GetGameState();
        if (currentState.IsGameOver)
            return Results.BadRequest(new { error = "game is already over" });

        game.Engine.EndGame();
        game.UpdatedAt = DateTimeOffset.UtcNow;

        var endedState = game.Engine.GetGameState();
        scores = endedState.Players
            .OrderByDescending(p => p.Score)
            .Select(p => new OnlineScoreEntry(p.Name, p.Score))
            .ToList();

        winner = scores.Count > 0 && scores.Count(s => s.Score == scores[0].Score) == 1
            ? scores[0].PlayerName
            : null;
    }

    var endedAt = DateTimeOffset.UtcNow;

    state.Publish(gameId, new ServerEvent("game.ended", new
    {
        gameId,
        endedAt,
        request.PlayerId,
        scores,
        winner
    }));

    return Results.Ok(new
    {
        gameId,
        isGameOver = true,
        winner,
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

static Dictionary<Position, char> BuildLetterPlacementsFromPayload(JsonElement? payload)
{
    if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        throw new GameException("Payload play.move invalide.");

    if (!payload.Value.TryGetProperty("position", out var positionProperty) ||
        !payload.Value.TryGetProperty("word", out var wordProperty) ||
        !payload.Value.TryGetProperty("direction", out var directionProperty))
        throw new GameException("Payload play.move incomplet.");

    var positionRaw = positionProperty.GetString();
    var word = wordProperty.GetString();
    var direction = directionProperty.GetString()?.Trim().ToUpperInvariant();

    if (string.IsNullOrWhiteSpace(positionRaw) || string.IsNullOrWhiteSpace(word) || (direction is not "H" and not "V"))
        throw new GameException("Payload play.move invalide.");

    if (!TryParsePosition(positionRaw, out var start))
        throw new GameException($"Position invalide: {positionRaw}");

    var placements = new Dictionary<Position, char>();
    for (var i = 0; i < word.Length; i++)
    {
        var pos = direction == "H"
            ? new Position(start.Row, start.Column + i)
            : new Position(start.Row + i, start.Column);
        placements[pos] = word[i];
    }

    return placements;
}

static bool TryParsePosition(string input, out Position position)
{
    position = new Position(0, 0);
    input = input.Trim().ToUpperInvariant();

    if (input.Length < 2)
        return false;

    var colChar = input[0];
    if (colChar < 'A' || colChar > 'O')
        return false;

    if (!int.TryParse(input[1..], out var row) || row < 1 || row > 15)
        return false;

    position = new Position(row - 1, colChar - 'A');
    return true;
}

static IReadOnlyList<OnlineBoardTile> CaptureBoard(BoardState board)
{
    var tiles = new List<OnlineBoardTile>();
    for (var row = 0; row < 15; row++)
        for (var col = 0; col < 15; col++)
        {
            var tile = board.Grid[row, col];
            if (tile is not null)
                tiles.Add(new OnlineBoardTile(row, col, tile.Letter, tile.IsWildcard));
        }

    return tiles;
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
    DateTimeOffset UpdatedAt,
    List<OnlinePlayer> Players,
    Dictionary<string, int> PlayerIndexById,
    List<OnlineMove> Moves,
    string? TournamentId,
    RankingQueue Queue,
    IGameEngine Engine)
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
}

public sealed record OnlinePlayer(string PlayerId, string PlayerName, bool IsHost);

public sealed record OnlineMove(
    string MoveId,
    string PlayerId,
    string PlayerName,
    string Command,
    JsonElement? Payload,
    DateTimeOffset PlayedAt,
    int Score = 0);

public sealed record OnlineScoreEntry(string PlayerName, int Score);
public sealed record OnlineBoardTile(int Row, int Column, char Letter, bool IsWildcard);

public sealed record ServerEvent(string Type, object Payload);

public sealed class GameHubState
{
    private static readonly IReadOnlyDictionary<char, int> FrenchDistribution = new Dictionary<char, int>
    {
        ['A'] = 9,  ['B'] = 2,  ['C'] = 2,  ['D'] = 3,  ['E'] = 15,
        ['F'] = 2,  ['G'] = 2,  ['H'] = 2,  ['I'] = 8,  ['J'] = 1,
        ['K'] = 1,  ['L'] = 5,  ['M'] = 3,  ['N'] = 6,  ['O'] = 6,
        ['P'] = 2,  ['Q'] = 1,  ['R'] = 6,  ['S'] = 6,  ['T'] = 6,
        ['U'] = 6,  ['V'] = 2,  ['W'] = 1,  ['X'] = 1,  ['Y'] = 1,
        ['Z'] = 1,  ['*'] = 2
    };

    private readonly IGameLanguageProvider _languageProvider;
    private readonly ConcurrentDictionary<string, OnlineGame> _games = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EventSubscribers> _subscribers = new(StringComparer.Ordinal);

    public GameHubState(IGameLanguageProvider languageProvider)
    {
        _languageProvider = languageProvider;
    }

    public IGameEngine CreateEngine() =>
        new GameEngine(
            _languageProvider.GetDictionary(),
            _languageProvider.GetLetterScores(),
            FrenchDistribution);

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


