using System.Text.Json;
using Lama.Contracts;
using Lama.Domain.Board;
using Lama.Domain.Engine;

namespace Lama.Server.Endpoints;

public static class GamesCommandEndpoints
{
    public static IEndpointRouteBuilder MapGamesCommandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games", CreateGame);
        app.MapPost("/api/games/{gameId}/join", JoinGame);
        app.MapPost("/api/games/{gameId}/moves", PlayMove);
        app.MapPost("/api/games/{gameId}/end", EndGame);
        app.MapGet("/api/games/{gameId}/events", StreamEventsAsync);
        return app;
    }

    private static IResult CreateGame(global::CreateGameRequest request, global::GameHubState state)
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

        var game = new global::OnlineGame(
            Id: gameId,
            GameLevel: level,
            BoardSize: request.BoardSize > 0 ? request.BoardSize : 15,
            RackSize: request.RackSize > 0 ? request.RackSize : 7,
            MinWordLength: request.MinWordLength > 0 ? request.MinWordLength : 2,
            Language: string.IsNullOrWhiteSpace(request.Language) ? "fr" : request.Language.Trim(),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Players: [new global::OnlinePlayer(hostId, hostName, true)],
            PlayerIndexById: new Dictionary<string, int>(StringComparer.Ordinal) { [hostId] = 0 },
            Moves: [],
            TournamentId: request.TournamentId,
            Queue: ResolveQueue(level),
            Engine: engine);

        state.Create(game);

        state.Publish(gameId, new global::ServerEvent("game.created", new
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

    private static IResult JoinGame(string gameId, global::JoinGameRequest request, global::GameHubState state)
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

            game.Players.Add(new global::OnlinePlayer(playerId, trimmedName, false));
            game.PlayerIndexById[playerId] = game.Players.Count - 1;
            game.UpdatedAt = DateTimeOffset.UtcNow;

            var newState = game.Engine.GetGameState();
            rack = newState.Players[game.PlayerIndexById[playerId]].Rack.ToList();
        }

        state.Publish(gameId, new global::ServerEvent("game.joined", new
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

    private static IResult PlayMove(string gameId, global::PlayMoveRequest request, global::GameHubState state)
    {
        if (!state.TryGet(gameId, out var game))
            return Results.NotFound(new { error = "game not found" });

        if (string.IsNullOrWhiteSpace(request.PlayerId))
            return Results.BadRequest(new { error = "playerId is required" });

        if (string.IsNullOrWhiteSpace(request.Command))
            return Results.BadRequest(new { error = "command is required" });

        var normalizedCommand = request.Command.Trim().ToLowerInvariant();
        global::OnlineMove createdMove;
        int score = 0;
        List<char>? newRack = null;
        int nextCurrentPlayerIndex;
        string? nextPlayerId;
        int playedTurn;
        List<global::OnlineMovePlacement> placements = [];
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
                            .Select(p => new global::OnlineMovePlacement(p.Row, p.Column, p.Letter))
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
                            .Select(p => new global::OnlineMovePlacement(p.Row, p.Column, p.Letter))
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
                            .Select(kv => new global::OnlineMovePlacement(kv.Key.Row, kv.Key.Column, char.ToUpperInvariant(kv.Value)))
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

            createdMove = new global::OnlineMove(
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

        state.Publish(gameId, new global::ServerEvent("game.move.played", new
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
    }

    private static IResult EndGame(string gameId, global::EndGameRequest request, global::GameHubState state)
    {
        if (!state.TryGet(gameId, out var game))
            return Results.NotFound(new { error = "game not found" });

        string? winner;
        List<global::OnlineScoreEntry> scores;

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
                .Select(p => new global::OnlineScoreEntry(p.Name, p.Score))
                .ToList();

            winner = scores.Count > 0 && scores.Count(s => s.Score == scores[0].Score) == 1
                ? scores[0].PlayerName
                : null;
        }

        var endedAt = DateTimeOffset.UtcNow;

        state.Publish(gameId, new global::ServerEvent("game.ended", new
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

    private static async Task StreamEventsAsync(string gameId, global::GameHubState state, HttpContext httpContext)
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

        await WriteEventAsync(httpContext, new global::ServerEvent("sse.connected", new
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
    }

    private static RankingQueue ResolveQueue(GameLevel level) => level switch
    {
        GameLevel.Casual => RankingQueue.CasualUnranked,
        GameLevel.Tournament => RankingQueue.Tournament,
        _ => RankingQueue.OpenRanked
    };

    private static async Task WriteEventAsync(HttpContext context, global::ServerEvent evt)
    {
        var payloadJson = JsonSerializer.Serialize(evt.Payload);
        await context.Response.WriteAsync($"event: {evt.Type}\\n");
        await context.Response.WriteAsync($"data: {payloadJson}\\n\\n");
        await context.Response.Body.FlushAsync();
    }

    private static Dictionary<Position, char> BuildLetterPlacementsFromPayload(JsonElement? payload)
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

    private static IReadOnlyList<char> BuildSwapLettersFromPayload(JsonElement? payload, IReadOnlyList<char> currentRack)
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

    private static bool TryParsePosition(string input, out Position position)
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
}

