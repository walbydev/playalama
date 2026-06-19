using Lama.Contracts;
using Lama.Domain.Engine;
using Lama.Server.Contracts.Api;
using Lama.Server.Runtime;

namespace Lama.Server.Endpoints;

public static class GamesCommandEndpoints
{
    public static IEndpointRouteBuilder MapGamesCommandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/games", CreateGame)
            .WithName("CreateGame")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/games/{gameId}/join", JoinGame)
            .WithName("JoinGame")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/games/{gameId}/moves", PlayMove)
            .WithName("PlayMove")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/games/{gameId}/end", EndGame)
            .WithName("EndGame")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/games/{gameId}/events", StreamEventsAsync)
            .WithName("StreamEvents")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult CreateGame(HttpContext context, CreateGameRequest request, GameHubState state)
    {
        // Vérifier l'authentification
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.HostName))
            return Results.BadRequest(new { error = "hostName is required" });

        var gameId = Guid.NewGuid().ToString("N");
        var hostId = context.GetPlayerId() ?? Guid.NewGuid().ToString("N");
        var hostName = request.HostName.Trim();
        var level = request.GameLevel ?? GameLevel.Standard;
        var boardSize = request.BoardSize > 0 ? request.BoardSize : 15;
        var rackSize = request.RackSize > 0 ? request.RackSize : 7;
        var language = string.IsNullOrWhiteSpace(request.Language) ? "fr" : request.Language.Trim();
        var gameType = string.IsNullOrWhiteSpace(request.TournamentId) ? "classic" : "tournament";

        var profile = new TileDistributionProfile(
            Language: language,
            BoardSize: boardSize,
            RackSize: rackSize,
            GameLevel: level,
            GameType: gameType);

        var engine = state.CreateEngine(profile);
        engine.InitializeGame([hostName]);
        var initialState = engine.GetGameState();

        var game = new OnlineGame(
            Id: gameId,
            GameLevel: level,
            BoardSize: boardSize,
            RackSize: rackSize,
            MinWordLength: request.MinWordLength > 0 ? request.MinWordLength : 2,
            Language: language,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Players: [new OnlinePlayer(hostId, hostName, true)],
            PlayerIndexById: new Dictionary<string, int>(StringComparer.Ordinal) { [hostId] = 0 },
            Moves: [],
            TournamentId: request.TournamentId,
            Queue: GamesEndpointParsers.ResolveQueue(level),
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
    }

    private static IResult JoinGame(HttpContext context, string gameId, JoinGameRequest request, GameHubState state)
    {
        // Vérifier l'authentification
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.PlayerName))
            return Results.BadRequest(new { error = "playerName is required" });

        if (!state.TryGet(gameId, out var game))
            return Results.NotFound(new { error = "game not found" });

        var trimmedName = request.PlayerName.Trim();
        var playerId = context.GetPlayerId() ?? Guid.NewGuid().ToString("N");
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
    }

    private static IResult PlayMove(HttpContext context, string gameId, PlayMoveRequest request, GameHubState state)
    {
        // Vérifier l'authentification
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

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
        int playedTurn;
        List<OnlineMovePlacement> placements = [];
        List<object> suggestions = [];
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
                        var letters = GamesEndpointParsers.BuildLetterPlacementsFromPayload(request.Payload);
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
                        var swapLetters = GamesEndpointParsers.BuildSwapLettersFromPayload(request.Payload, currentState.Players[playerIndex].Rack);
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

                        placements = challengeResult.ChallengedMove?.Placements
                            .Select(p => new OnlineMovePlacement(p.Row, p.Column, p.Letter))
                            .ToList() ?? [];
                        score = 0;
                        newRack = challengeResult.GameState.Players[playerIndex].Rack.ToList();
                        break;

                    case "play.check":
                        var simulatedLetters = GamesEndpointParsers.BuildLetterPlacementsFromPayload(request.Payload);
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

                    case "play.suggest":
                        // Stub online: aucune mutation de partie, suggestions vides pour l'instant.
                        score = 0;
                        newRack = currentState.Players[playerIndex].Rack.ToList();
                        suggestions = [];
                        actionMessage = "Aucune suggestion disponible (stub).";
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
            suggestions,
            currentPlayerIndex = nextCurrentPlayerIndex,
            nextPlayerId,
            message = actionMessage,
            challengeSucceeded
        });
    }

    private static IResult EndGame(HttpContext context, string gameId, EndGameRequest request, GameHubState state)
    {
        // Vérifier l'authentification
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

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
    }

    private static async Task StreamEventsAsync(string gameId, GameHubState state, HttpContext httpContext)
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

        await GamesEndpointParsers.WriteEventAsync(httpContext, new ServerEvent("sse.connected", new
        {
            gameId,
            utcNow = DateTimeOffset.UtcNow
        }));

        try
        {
            while (!httpContext.RequestAborted.IsCancellationRequested)
            {
                while (subscription.Reader.TryRead(out var evt))
                    await GamesEndpointParsers.WriteEventAsync(httpContext, evt);

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
    }
}

