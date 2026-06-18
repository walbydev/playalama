using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Lama.Contracts;
using Lama.Server.Data;
using Lama.Server.Endpoints;
using Lama.Domain.Board;
using Lama.Domain.Engine;
using Lama.Languages.fr;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LamaServerDb")
    ?? "Host=localhost;Port=5432;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me";

builder.Services.AddDbContext<LamaDbContext>(options =>
    options.UseNpgsql(connectionString));

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

app.MapHealthEndpoints();
app.MapInternalEndpoints(allowShutdown);

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

app.MapGet("/api/games", async (GameHubState state, LamaDbContext db, CancellationToken cancellationToken) =>
{
    var merged = new Dictionary<string, OnlineGameListItem>(StringComparer.Ordinal);
    var persistedPlayerCountsByGame = new Dictionary<Guid, int>();
    var persistedMoveCountsByGame = new Dictionary<Guid, int>();

    foreach (var game in state.ListGames())
    {
        lock (game)
        {
            var stateSnapshot = game.Engine.GetGameState();
            var status = stateSnapshot.IsGameOver ? "ended" : "active";

            merged[game.Id] = new OnlineGameListItem(
                Id: game.Id,
                GameLevel: game.GameLevel,
                Queue: game.Queue,
                BoardSize: game.BoardSize,
                RackSize: game.RackSize,
                MinWordLength: game.MinWordLength,
                Language: game.Language,
                Status: status,
                IsGameOver: stateSnapshot.IsGameOver,
                Players: game.Players.Count,
                Moves: game.Moves.Count,
                CreatedAt: game.CreatedAt,
                UpdatedAt: game.UpdatedAt,
                Source: "memory");
        }
    }

    var persistedGames = await db.SessionGames
        .AsNoTracking()
        .OrderByDescending(x => x.UpdatedAt)
        .ToListAsync(cancellationToken);

    try
    {
        persistedPlayerCountsByGame = await db.SessionPlayersInGame
            .AsNoTracking()
            .GroupBy(x => x.GameId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        persistedMoveCountsByGame = await db.SessionTurnLogs
            .AsNoTracking()
            .GroupBy(x => x.GameId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }
    catch (PostgresException ex) when (IsMissingDatabaseObject(ex))
    {
        // Backward compatibility: DB may still be at the minimal sessions.games schema.
    }

    foreach (var persistedGame in persistedGames)
    {
        var gameId = persistedGame.GameId.ToString("N");
        if (merged.ContainsKey(gameId))
            continue;

        var parsedLevel = ParseGameLevelToken(persistedGame.GameLevel);
        var parsedQueue = ParseRankingQueueToken(persistedGame.Queue);
        var normalizedStatus = NormalizeStatusToken(persistedGame.Status);
        var isGameOver = string.Equals(normalizedStatus, "ended", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(normalizedStatus, "abandoned", StringComparison.OrdinalIgnoreCase);

        merged[gameId] = new OnlineGameListItem(
            Id: gameId,
            GameLevel: parsedLevel,
            Queue: parsedQueue,
            BoardSize: persistedGame.BoardSize,
            RackSize: persistedGame.RackSize,
            MinWordLength: persistedGame.MinWordLength,
            Language: persistedGame.Language,
            Status: normalizedStatus,
            IsGameOver: isGameOver,
            Players: persistedPlayerCountsByGame.GetValueOrDefault(persistedGame.GameId, 0),
            Moves: persistedMoveCountsByGame.GetValueOrDefault(persistedGame.GameId, 0),
            CreatedAt: persistedGame.CreatedAt,
            UpdatedAt: persistedGame.UpdatedAt,
            Source: "database");
    }

    var ordered = merged.Values
        .OrderByDescending(x => x.UpdatedAt)
        .ToList();

    return Results.Ok(new
    {
        total = ordered.Count,
        games = ordered
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
    int playedTurn = 0;
    List<OnlineMovePlacement> placements = [];
    string? actionMessage = null;
    bool? challengeSucceeded = null;

    lock (game)
    {
        var currentState = game.Engine.GetGameState();
        playedTurn = currentState.TurnNumber;

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

                    var stateAfterMove = game.Engine.PlayMove(letters);
                    var historyEntry = stateAfterMove.History.LastOrDefault()
                        ?? throw new GameException("Historique moteur introuvable apres play.move.");

                    playedTurn = historyEntry.TurnNumber;
                    score = historyEntry.Score;
                    placements = historyEntry.Placements
                        .Select(p => new OnlineMovePlacement(p.Row, p.Column, p.Letter))
                        .ToList();
                    newRack = stateAfterMove.Players[playerIndex].Rack.ToList();
                    break;

                case "play.swap":
                    var swapLetters = BuildSwapLettersFromPayload(request.Payload, currentState.Players[playerIndex].Rack);
                    game.Engine.SwapLetters(swapLetters);
                    var stateAfterSwap = game.Engine.GetGameState();
                    newRack = stateAfterSwap.Players[playerIndex].Rack.ToList();
                    break;

                case "play.challenge":
                    var challengeResult = game.Engine.ChallengeLastMove();
                    challengeSucceeded = challengeResult.ChallengeSucceeded;
                    actionMessage = challengeResult.Message;

                    if (challengeResult.ChallengeSucceeded)
                    {
                        var lastPlayableMoveIndex = game.Moves.FindLastIndex(m => m.Command == "play.move");
                        if (lastPlayableMoveIndex >= 0)
                            game.Moves.RemoveAt(lastPlayableMoveIndex);
                    }

                    placements = challengeResult.ChallengedMove.Placements
                        .Select(p => new OnlineMovePlacement(p.Row, p.Column, p.Letter))
                        .ToList();
                    score = 0;
                    newRack = challengeResult.GameState.Players[playerIndex].Rack.ToList();
                    break;

                case "play.check":
                    var simulatedLetters = BuildLetterPlacementsFromPayload(request.Payload);
                    var simulatedValidation = game.Engine.ValidateMove(simulatedLetters);
                    if (!simulatedValidation.IsValid)
                        return Results.BadRequest(new { error = simulatedValidation.ErrorMessage });

                    score = simulatedValidation.Score;
                    placements = simulatedLetters
                        .OrderBy(kv => kv.Key.Row)
                        .ThenBy(kv => kv.Key.Column)
                        .Select(kv => new OnlineMovePlacement(kv.Key.Row, kv.Key.Column, char.ToUpperInvariant(kv.Value)))
                        .ToList();
                    newRack = currentState.Players[playerIndex].Rack.ToList();
                    actionMessage = $"Coup valide : {score} pts";
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
            TurnNumber: playedTurn,
            Placements: placements,
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
        createdMove.TurnNumber,
        createdMove.Placements,
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
        nextPlayerId,
        message = actionMessage,
        challengeSucceeded
    });
});

app.MapGet("/api/games/{gameId}", async (string gameId, GameHubState state, LamaDbContext db, CancellationToken cancellationToken) =>
{
    // Priority to in-memory state to preserve the current online flow.
    if (state.TryGet(gameId, out var game))
    {
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
                moves = game.Moves,
                source = "memory"
            });
        }
    }

    // EF fallback for read-only persisted metadata.
    if (!Guid.TryParse(gameId, out var gameGuid))
        return Results.NotFound(new { error = "game not found" });

    var persistedGame = await db.SessionGames
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.GameId == gameGuid, cancellationToken);

    if (persistedGame is null)
        return Results.NotFound(new { error = "game not found" });

    var parsedLevel = ParseGameLevelToken(persistedGame.GameLevel);
    var parsedQueue = ParseRankingQueueToken(persistedGame.Queue);
    var isGameOver = string.Equals(persistedGame.Status, "ended", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(persistedGame.Status, "abandoned", StringComparison.OrdinalIgnoreCase);

    var persistedPlayers = new List<object>();
    var persistedMoves = new List<object>();
    IReadOnlyList<OnlineBoardTile> persistedBoard = [];
    var lastTurnNumber = 0;

    try
    {
        var dbPlayers = await db.SessionPlayersInGame
            .AsNoTracking()
            .Where(x => x.GameId == gameGuid)
            .OrderBy(x => x.PlayerIndex)
            .ToListAsync(cancellationToken);

        var playersBySessionId = dbPlayers.ToDictionary(x => x.PlayerSessionId, x => x);
        persistedPlayers = dbPlayers
            .Select(x => (object)new
            {
                PlayerId = x.PlayerId.ToString("N"),
                PlayerName = x.Nickname,
                x.IsHost,
                Score = 0,
                Rack = Array.Empty<char>(),
                RackCount = 0
            })
            .ToList();

        var dbTurns = await db.SessionTurnLogs
            .AsNoTracking()
            .Where(x => x.GameId == gameGuid)
            .OrderBy(x => x.TurnNumber)
            .ThenBy(x => x.ExecutedAt)
            .ToListAsync(cancellationToken);

        lastTurnNumber = dbTurns.Count == 0 ? 0 : dbTurns.Max(x => x.TurnNumber);

        persistedMoves = dbTurns
            .Select(x =>
            {
                var payload = ParseActionPayload(x.ActionPayload);
                playersBySessionId.TryGetValue(x.PlayerSessionId, out var owner);

                return (object)new
                {
                    MoveId = x.TurnId.ToString("N"),
                    PlayerId = owner?.PlayerId.ToString("N") ?? x.PlayerSessionId.ToString("N"),
                    PlayerName = owner?.Nickname ?? "unknown",
                    Command = ToOnlineCommand(x.ActionType),
                    Payload = payload,
                    PlayedAt = x.ExecutedAt,
                    Score = ExtractScoreFromPayload(payload),
                    TurnNumber = x.TurnNumber,
                    Placements = ExtractPlacementsFromPayload(payload)
                };
            })
            .ToList();

        var dbBoardState = await db.SessionBoardStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameId == gameGuid, cancellationToken);

        if (dbBoardState is not null)
            persistedBoard = ParseBoardTilesFromJson(dbBoardState.BoardJson);
    }
    catch (PostgresException ex) when (IsMissingDatabaseObject(ex))
    {
        // Backward compatibility: DB may still be at the minimal sessions.games schema.
    }

    var currentPlayerIndex = persistedPlayers.Count == 0
        ? 0
        : lastTurnNumber % persistedPlayers.Count;

    return Results.Ok(new
    {
        Id = persistedGame.GameId.ToString("N"),
        GameLevel = parsedLevel,
        Queue = parsedQueue,
        persistedGame.BoardSize,
        persistedGame.RackSize,
        persistedGame.MinWordLength,
        persistedGame.Language,
        TournamentId = (string?)null,
        persistedGame.CreatedAt,
        persistedGame.UpdatedAt,
        IsGameOver = isGameOver,
        CurrentPlayerIndex = currentPlayerIndex,
        TurnNumber = lastTurnNumber,
        players = persistedPlayers,
        board = persistedBoard,
        moves = persistedMoves,
        source = "database"
    });
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

static GameLevel ParseGameLevelToken(string token)
{
    if (Enum.TryParse<GameLevel>(token, ignoreCase: true, out var parsed))
        return parsed;

    return GameLevel.Standard;
}

static RankingQueue ParseRankingQueueToken(string token)
{
    return token.Trim().ToLowerInvariant() switch
    {
        "open" => RankingQueue.OpenRanked,
        "tournament" => RankingQueue.Tournament,
        "global" => RankingQueue.GlobalPrestige,
        "casual" => RankingQueue.CasualUnranked,
        _ => RankingQueue.OpenRanked
    };
}

static string NormalizeStatusToken(string? token)
{
    if (string.IsNullOrWhiteSpace(token))
        return "unknown";

    return token.Trim().ToLowerInvariant();
}

static string ToOnlineCommand(string? actionType)
{
    if (string.IsNullOrWhiteSpace(actionType))
        return "play.move";

    return actionType.Trim().ToLowerInvariant() switch
    {
        "move" => "play.move",
        "pass" => "play.pass",
        "swap" => "play.swap",
        "challenge" => "play.challenge",
        "check" => "play.check",
        var other => $"play.{other}"
    };
}

static JsonElement? ParseActionPayload(string? payload)
{
    if (string.IsNullOrWhiteSpace(payload))
        return null;

    try
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }
    catch
    {
        return null;
    }
}

static int ExtractScoreFromPayload(JsonElement? payload)
{
    if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        return 0;

    if (!payload.Value.TryGetProperty("score", out var scoreElement))
        return 0;

    return scoreElement.ValueKind switch
    {
        JsonValueKind.Number when scoreElement.TryGetInt32(out var score) => score,
        JsonValueKind.String when int.TryParse(scoreElement.GetString(), out var score) => score,
        _ => 0
    };
}

static IReadOnlyList<OnlineMovePlacement> ExtractPlacementsFromPayload(JsonElement? payload)
{
    if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        return [];

    if (!payload.Value.TryGetProperty("placements", out var placementsElement) ||
        placementsElement.ValueKind != JsonValueKind.Array)
        return [];

    var placements = new List<OnlineMovePlacement>();

    foreach (var item in placementsElement.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object)
            continue;

        if (!item.TryGetProperty("row", out var rowElement) || !rowElement.TryGetInt32(out var row))
            continue;
        if (!item.TryGetProperty("column", out var columnElement) || !columnElement.TryGetInt32(out var column))
            continue;
        if (!item.TryGetProperty("letter", out var letterElement))
            continue;

        var letterRaw = letterElement.ValueKind switch
        {
            JsonValueKind.String => letterElement.GetString(),
            _ => letterElement.ToString()
        };

        if (string.IsNullOrWhiteSpace(letterRaw))
            continue;

        placements.Add(new OnlineMovePlacement(row, column, letterRaw[0]));
    }

    return placements;
}

static IReadOnlyList<OnlineBoardTile> ParseBoardTilesFromJson(string? boardJson)
{
    if (string.IsNullOrWhiteSpace(boardJson))
        return [];

    try
    {
        using var document = JsonDocument.Parse(boardJson);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
            return ParseBoardTilesFromArray(root);

        if (root.ValueKind != JsonValueKind.Object)
            return [];

        if (root.TryGetProperty("tiles", out var tilesElement) && tilesElement.ValueKind == JsonValueKind.Array)
            return ParseBoardTilesFromArray(tilesElement);

        if (root.TryGetProperty("grid", out var gridElement) && gridElement.ValueKind == JsonValueKind.Array)
            return ParseBoardTilesFromGrid(gridElement);

        return [];
    }
    catch
    {
        return [];
    }
}

static IReadOnlyList<OnlineBoardTile> ParseBoardTilesFromArray(JsonElement tilesElement)
{
    var tiles = new List<OnlineBoardTile>();

    foreach (var tileElement in tilesElement.EnumerateArray())
    {
        if (tileElement.ValueKind != JsonValueKind.Object)
            continue;

        if (!TryGetIntProperty(tileElement, "row", out var row))
            continue;
        if (!TryGetIntProperty(tileElement, "column", out var column))
            continue;
        if (!TryGetLetterProperty(tileElement, "letter", out var letter))
            continue;

        var isWildcard = TryGetBoolProperty(tileElement, "isWildcard", out var wildcard) && wildcard;
        tiles.Add(new OnlineBoardTile(row, column, letter, isWildcard));
    }

    return tiles;
}

static IReadOnlyList<OnlineBoardTile> ParseBoardTilesFromGrid(JsonElement gridElement)
{
    var tiles = new List<OnlineBoardTile>();
    var rowIndex = 0;

    foreach (var rowElement in gridElement.EnumerateArray())
    {
        if (rowElement.ValueKind != JsonValueKind.Array)
        {
            rowIndex++;
            continue;
        }

        var columnIndex = 0;
        foreach (var cellElement in rowElement.EnumerateArray())
        {
            if (cellElement.ValueKind == JsonValueKind.Object &&
                TryGetLetterProperty(cellElement, "letter", out var letter))
            {
                var isWildcard = TryGetBoolProperty(cellElement, "isWildcard", out var wildcard) && wildcard;
                tiles.Add(new OnlineBoardTile(rowIndex, columnIndex, letter, isWildcard));
            }

            columnIndex++;
        }

        rowIndex++;
    }

    return tiles;
}

static bool TryGetIntProperty(JsonElement element, string name, out int value)
{
    value = 0;
    if (!element.TryGetProperty(name, out var property))
        return false;

    return property.ValueKind switch
    {
        JsonValueKind.Number when property.TryGetInt32(out value) => true,
        JsonValueKind.String when int.TryParse(property.GetString(), out value) => true,
        _ => false
    };
}

static bool TryGetBoolProperty(JsonElement element, string name, out bool value)
{
    value = false;
    if (!element.TryGetProperty(name, out var property))
        return false;

    if (property.ValueKind == JsonValueKind.True)
    {
        value = true;
        return true;
    }

    if (property.ValueKind == JsonValueKind.False)
    {
        value = false;
        return true;
    }

    return property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out value);
}

static bool TryGetLetterProperty(JsonElement element, string name, out char value)
{
    value = default;
    if (!element.TryGetProperty(name, out var property))
        return false;

    var raw = property.ValueKind switch
    {
        JsonValueKind.String => property.GetString(),
        _ => property.ToString()
    };

    if (string.IsNullOrWhiteSpace(raw))
        return false;

    value = raw[0];
    return true;
}

static bool IsMissingDatabaseObject(PostgresException ex) =>
    ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn;

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

static IReadOnlyList<char> BuildSwapLettersFromPayload(JsonElement? payload, IReadOnlyList<char> currentRack)
{
    if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        throw new GameException("Payload play.swap invalide.");

    var swapAll = false;
    if (payload.Value.TryGetProperty("swapAll", out var swapAllProperty))
    {
        swapAll = swapAllProperty.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(swapAllProperty.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    if (swapAll)
        return currentRack.ToList();

    if (!payload.Value.TryGetProperty("letters", out var lettersProperty))
        throw new GameException("Payload play.swap incomplet (letters requis sans swapAll).");

    var lettersRaw = lettersProperty.ValueKind switch
    {
        JsonValueKind.String => lettersProperty.GetString(),
        _ => lettersProperty.ToString()
    };

    if (string.IsNullOrWhiteSpace(lettersRaw))
        throw new GameException("Aucune lettre fournie pour play.swap.");

    return lettersRaw
        .Trim()
        .ToUpperInvariant()
        .ToCharArray();
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
    int TurnNumber,
    IReadOnlyList<OnlineMovePlacement> Placements,
    int Score = 0);

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

public sealed record OnlineScoreEntry(string PlayerName, int Score);
public sealed record OnlineBoardTile(int Row, int Column, char Letter, bool IsWildcard);
public sealed record OnlineMovePlacement(int Row, int Column, char Letter);

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

    public IReadOnlyList<OnlineGame> ListGames() => _games.Values.ToList();

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


