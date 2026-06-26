using Lama.Contracts;
using Lama.Server.Bots;
using Lama.Server.Contracts.Api;
using Lama.Server.Runtime;
using Lama.Server.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        app.MapPost("/games/{gameId}/abandon", AbandonGame)
            .WithName("AbandonGame")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/games/{gameId}/start", StartGame)
            .WithName("StartGame")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
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
        var usesLobby = request.Mode is not null
                        || !string.IsNullOrWhiteSpace(request.GameName)
                        || request.IsPrivate
                        || request.MaxPlayers is not null
                        || request.EnableAi;

        var mode = request.Mode ?? OnlineGameMode.Multi;
        if (mode == OnlineGameMode.Multi && request.RackSize <= 0)
            return Results.BadRequest(new { error = "invalid rackSize" });

        var requestedMaxPlayers = request.MaxPlayers ?? (mode == OnlineGameMode.Multi ? 4 : 1);
        if (requestedMaxPlayers < 1)
            return Results.BadRequest(new { error = "maxPlayers must be >= 1" });

        if (mode == OnlineGameMode.Multi && (requestedMaxPlayers < 2 || requestedMaxPlayers > 4))
            return Results.BadRequest(new { error = "multi mode supports between 2 and 4 participants" });

        if (mode == OnlineGameMode.Solo && requestedMaxPlayers > 2)
            return Results.BadRequest(new { error = "solo mode supports at most host + 1 AI" });

        if (mode == OnlineGameMode.Multi && request.EnableAi && requestedMaxPlayers < 2)
            return Results.BadRequest(new { error = "invalid AI slot configuration" });

        // Le bot est injecté comme joueur réel : plus besoin de slot réservé
        var reservedAiSlots = 0;
        var maxPlayers = mode == OnlineGameMode.Solo
            ? Math.Min(2, Math.Max(1, requestedMaxPlayers))
            : requestedMaxPlayers;

        var gameName = string.IsNullOrWhiteSpace(request.GameName)
            ? GenerateRandomGameName()
            : request.GameName.Trim();

        if (IsGameNameTaken(state, gameName))
            return Results.BadRequest(new { error = "game name already in use" });

        if (!request.IsPrivate && !string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "password requires private game" });

        if (request.IsPrivate && string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "password is required for private game" });

        if (!state.TryReservePlayerForGame(hostId, gameId, out var blockingHostGameId))
            return Results.BadRequest(new { error = $"player already active in game '{blockingHostGameId}'" });

        var passwordHash = request.IsPrivate
            ? ComputePasswordHash(request.Password!)
            : null;

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

        // ── Joueurs initiaux ──────────────────────────────────────────────────
        var initialPlayers = new List<OnlinePlayer>
        {
            new(hostId, hostName, IsHost: true)
        };
        var playerIndexById = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [hostId] = 0
        };

        // Injection du bot si demandée
        BotProfile? botProfile = null;
        if (request.EnableAi)
        {
            botProfile = BotCatalog.Default;
            initialPlayers.Add(new OnlinePlayer(botProfile.BotId, botProfile.Name, IsHost: false, IsBot: true));
            playerIndexById[botProfile.BotId] = 1;
        }

        // Initialiser le moteur avec tous les joueurs (humains + bot)
        engine.InitializeGame(initialPlayers.Select(p => p.PlayerName).ToList());
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
            Players: initialPlayers,
            PlayerIndexById: playerIndexById,
            Moves: [],
            TournamentId: request.TournamentId,
            Queue: GamesEndpointParsers.ResolveQueue(level),
            Engine: engine,
            Mode: mode,
            GameName: gameName,
            IsPrivate: request.IsPrivate,
            PasswordHash: passwordHash,
            MaxPlayers: maxPlayers,
            ReservedAiSlots: reservedAiSlots,
            HasStarted: mode == OnlineGameMode.Solo || !usesLobby,
            UsesLobby: usesLobby,
            IsClosed: false);

        state.Create(game);

        state.Publish(gameId, new ServerEvent("game.created", new
        {
            gameId,
            hostPlayerId = hostId,
            gameName,
            mode,
            level,
            queue = game.Queue,
            isPrivate = game.IsPrivate,
            maxPlayers = game.MaxPlayers,
            reservedAiSlots = game.ReservedAiSlots,
            hasStarted = game.HasStarted,
            usesLobby = game.UsesLobby,
            rack = initialState.Players[0].Rack,
            createdAt = game.CreatedAt
        }));

        return Results.Ok(new
        {
            gameId,
            hostPlayerId = hostId,
            gameName,
            mode,
            game.GameLevel,
            game.Queue,
            game.BoardSize,
            game.RackSize,
            game.MinWordLength,
            game.Language,
            game.IsPrivate,
            game.MaxPlayers,
            game.ReservedAiSlots,
            game.HasStarted,
            game.UsesLobby,
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

            if (game.IsClosed)
                return Results.BadRequest(new { error = "game is closed" });

            if (game.Mode == OnlineGameMode.Solo)
                return Results.BadRequest(new { error = "solo games cannot be joined" });

            if (game.UsesLobby && game.HasStarted)
                return Results.BadRequest(new { error = "cannot join a game that has already started" });

            var occupiedSlots = game.Players.Count + game.ReservedAiSlots;
            if (occupiedSlots >= game.MaxPlayers)
                return Results.BadRequest(new { error = "game is full" });

            if (game.IsPrivate)
            {
                if (string.IsNullOrWhiteSpace(request.Password) ||
                    !VerifyPassword(request.Password, game.PasswordHash))
                    return Results.BadRequest(new { error = "invalid game password" });
            }

            if (game.PlayerIndexById.ContainsKey(playerId))
                return Results.BadRequest(new { error = "player already joined this game" });

            if (!state.TryReservePlayerForGame(playerId, gameId, out var blockingGameId))
                return Results.BadRequest(new { error = $"player already active in game '{blockingGameId}'" });

            if (currentState.History.Count > 0)
                return Results.BadRequest(new { error = "cannot join a game that has already started" });

            try
            {
                var allPlayerNames = game.Players.Select(p => p.PlayerName).ToList();
                allPlayerNames.Add(trimmedName);
                game.Engine.InitializeGame(allPlayerNames);

                game.Players.Add(new OnlinePlayer(playerId, trimmedName, false));
                game.PlayerIndexById[playerId] = game.Players.Count - 1;
                game.UpdatedAt = DateTimeOffset.UtcNow;

                var newState = game.Engine.GetGameState();
                rack = newState.Players[game.PlayerIndexById[playerId]].Rack.ToList();
            }
            catch
            {
                state.ReleasePlayerReservation(playerId, gameId);
                throw;
            }
        }

        state.Publish(gameId, new ServerEvent("game.joined", new
        {
            gameId,
            playerId,
            playerName = trimmedName,
            players = game.Players.Count,
            maxPlayers = game.MaxPlayers,
            reservedAiSlots = game.ReservedAiSlots,
            hasStarted = game.HasStarted,
            usesLobby = game.UsesLobby
        }));

        return Results.Ok(new
        {
            gameId,
            playerId,
            players = game.Players.Count,
            game.GameLevel,
            game.Queue,
            rack,
            game.Mode,
            game.IsPrivate,
            game.MaxPlayers,
            game.ReservedAiSlots,
            game.HasStarted,
            game.UsesLobby
        });
    }

    private static IResult StartGame(HttpContext context, string gameId, StartGameRequest request, GameHubState state)
    {
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

        if (!state.TryGet(gameId, out var game))
            return Results.NotFound(new { error = "game not found" });

        var callerPlayerId = context.GetPlayerId();
        if (string.IsNullOrWhiteSpace(callerPlayerId))
            return Results.Unauthorized();

        lock (game)
        {
            if (!game.UsesLobby)
                return Results.BadRequest(new { error = "game does not require explicit start" });

            if (game.IsClosed)
                return Results.BadRequest(new { error = "game is closed" });

            var host = game.Players.FirstOrDefault(p => p.IsHost);
            if (host is null || !string.Equals(host.PlayerId, callerPlayerId, StringComparison.Ordinal))
                return Results.BadRequest(new { error = "only host can start the game" });

            if (game.HasStarted)
                return Results.BadRequest(new { error = "game already started" });

            var stateSnapshot = game.Engine.GetGameState();
            if (stateSnapshot.IsGameOver)
                return Results.BadRequest(new { error = "game is over" });

            var occupiedSlots = game.Players.Count + game.ReservedAiSlots;
            if (!request.Force && occupiedSlots < 2)
                return Results.BadRequest(new { error = "at least two participants are required to start" });

            game.HasStarted = true;
            game.UpdatedAt = DateTimeOffset.UtcNow;
        }

        state.Publish(gameId, new ServerEvent("game.started", new
        {
            gameId,
            startedAt = DateTimeOffset.UtcNow,
            game.Mode,
            game.MaxPlayers,
            game.ReservedAiSlots
        }));

        return Results.Ok(new
        {
            gameId,
            hasStarted = true,
            game.Mode,
            game.MaxPlayers,
            game.ReservedAiSlots
        });
    }

    private static async Task<IResult> PlayMove(HttpContext context, string gameId, PlayMoveRequest request, GameHubState state, IAISuggestionClient aiClient, BotAutoPlayService botAutoPlay)
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

        // play.suggest : lecture rapide de l'état puis calcul hors lock avec timeout
        if (normalizedCommand == "play.suggest")
            return await HandleSuggestAsync(gameId, request.PlayerId, request.Payload, game, aiClient);

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

            if (game.IsClosed)
                return Results.BadRequest(new { error = "game is closed" });

            if (game.UsesLobby && !game.HasStarted)
                return Results.BadRequest(new { error = "game has not started yet" });

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

                    default:
                        return Results.BadRequest(new { error = $"unsupported command: {normalizedCommand}" });
                }
            }
            catch (GameException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            // play.check = simulation pure, aucune mutation ni événement
            if (normalizedCommand == "play.check")
            {
                return Results.Ok(new { gameId, score, message = actionMessage });
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

        // ── Tour du bot : si le prochain joueur est une IA, jouer automatiquement ──
        var nextPlayer = game.Players.ElementAtOrDefault(nextCurrentPlayerIndex);
        if (nextPlayer?.IsBot == true)
        {
            var botProfile = BotCatalog.Find(nextPlayer.PlayerId);
            if (botProfile is not null)
            {
                _ = Task.Run(async () =>
                {
                    // Petite pause pour un rendu naturel côté client
                    await Task.Delay(600, CancellationToken.None);

                    var (botMove, _) = await botAutoPlay.AutoPlayAsync(
                        game, botProfile, aiClient, CancellationToken.None);

                    if (botMove is null) return;

                    int botNextIndex;
                    string? botNextPlayerId;

                    lock (game)
                    {
                        game.Moves.Add(botMove);
                        game.UpdatedAt = DateTimeOffset.UtcNow;
                        var botState = game.Engine.GetGameState();
                        botNextIndex    = botState.CurrentPlayerIndex;
                        botNextPlayerId = game.Players.ElementAtOrDefault(botNextIndex)?.PlayerId;
                    }

                    state.Publish(gameId, new ServerEvent("game.move.played", new
                    {
                        gameId,
                        botMove.MoveId,
                        botMove.PlayerId,
                        botMove.PlayerName,
                        botMove.Command,
                        botMove.TurnNumber,
                        botMove.Placements,
                        botMove.Score,
                        botMove.Payload,
                        botMove.PlayedAt,
                        currentPlayerIndex = botNextIndex,
                        nextPlayerId       = botNextPlayerId
                    }));
                });
            }
        }

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

    private static IResult AbandonGame(HttpContext context, string gameId, AbandonGameRequest request, GameHubState state)
    {
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

        if (!state.TryGet(gameId, out var game))
            return Results.NotFound(new { error = "game not found" });

        var callerPlayerId = context.GetPlayerId();
        if (string.IsNullOrWhiteSpace(callerPlayerId))
            return Results.Unauthorized();

        bool isGameOver;
        string? winner;
        List<OnlineScoreEntry> scores;
        string abandonedPlayerName;
        bool isHost;

        lock (game)
        {
            var currentState = game.Engine.GetGameState();
            if (currentState.IsGameOver)
                return Results.BadRequest(new { error = "game is already over" });

            if (!game.PlayerIndexById.TryGetValue(callerPlayerId, out var playerIndex))
                return Results.BadRequest(new { error = "unknown playerId" });

            var player = game.Players.ElementAtOrDefault(playerIndex);
            if (player is null)
                return Results.BadRequest(new { error = "player not found" });

            abandonedPlayerName = player.PlayerName;
            isHost = player.IsHost;

            if (isHost)
            {
                // L'hôte abandonne → fin de partie pour tout le monde
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
                game.IsClosed = true;
                game.EndReason = "abandoned";
                game.AbandonedByName = abandonedPlayerName;
                isGameOver = true;
            }
            else
            {
                // Non-hôte abandonne → il quitte, la partie continue
                game.AbandonedPlayerIds.Add(callerPlayerId);
                game.UpdatedAt = DateTimeOffset.UtcNow;

                // Si c'est son tour, passer automatiquement jusqu'au prochain joueur actif
                var maxPasses = game.Players.Count;
                var passes = 0;
                while (passes < maxPasses)
                {
                    var st = game.Engine.GetGameState();
                    var curId = game.Players.ElementAtOrDefault(st.CurrentPlayerIndex)?.PlayerId;
                    if (curId is null || !game.AbandonedPlayerIds.Contains(curId))
                        break;
                    game.Engine.PassTurn();
                    passes++;
                }

                // Si tous les joueurs restants sauf 1 ont abandonné → fin de partie
                var activeCount = game.Players.Count - game.AbandonedPlayerIds.Count;
                if (activeCount <= 1)
                {
                    game.Engine.EndGame();
                    var endedState = game.Engine.GetGameState();
                    scores = endedState.Players
                        .OrderByDescending(p => p.Score)
                        .Select(p => new OnlineScoreEntry(p.Name, p.Score))
                        .ToList();
                    winner = scores.Count > 0 && scores.Count(s => s.Score == scores[0].Score) == 1
                        ? scores[0].PlayerName
                        : null;
                    game.IsClosed = true;
                    game.EndReason = "abandoned";
                    game.AbandonedByName = abandonedPlayerName;
                    isGameOver = true;
                }
                else
                {
                    scores = game.Engine.GetGameState().Players
                        .OrderByDescending(p => p.Score)
                        .Select(p => new OnlineScoreEntry(p.Name, p.Score))
                        .ToList();
                    winner = null;
                    isGameOver = false;
                }
            }
        }

        if (isGameOver)
        {
            var releasedPlayerIds = game.Players.Select(p => p.PlayerId).ToList();
            state.ReleaseAllPlayerReservations(gameId, releasedPlayerIds);

            state.Publish(gameId, new ServerEvent("game.ended", new
            {
                gameId,
                endedAt = DateTimeOffset.UtcNow,
                reason = "abandoned",
                abandonedBy = abandonedPlayerName,
                scores,
                winner
            }));
        }
        else
        {
            state.ReleasePlayerReservation(callerPlayerId, gameId);

            state.Publish(gameId, new ServerEvent("player.abandoned", new
            {
                gameId,
                playerId = callerPlayerId,
                playerName = abandonedPlayerName
            }));
        }

        return Results.Ok(new
        {
            gameId,
            abandoned = true,
            isGameOver,
            winner = isGameOver ? winner : null,
            scores = isGameOver ? scores : null
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
        List<string> releasedPlayerIds;

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

            releasedPlayerIds = game.Players.Select(p => p.PlayerId).ToList();
            game.IsClosed = true;
        }

        state.ReleaseAllPlayerReservations(gameId, releasedPlayerIds);

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

    private static bool IsGameNameTaken(GameHubState state, string candidate)
    {
        foreach (var game in state.ListGames())
        {
            lock (game)
            {
                if (game.IsClosed || game.Engine.GetGameState().IsGameOver)
                    continue;

                if (string.Equals(game.GameName, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static async Task<IResult> HandleSuggestAsync(
        string gameId,
        string playerId,
        JsonElement? payload,
        OnlineGame game,
        IAISuggestionClient aiClient)
    {
        // Lecture de l'état sous lock bref — on relâche avant le calcul
        GameState currentState;
        int playerIndex;
        bool isFirstMove;

        lock (game)
        {
            currentState = game.Engine.GetGameState();

            if (currentState.IsGameOver)
                return Results.BadRequest(new { error = "game is over" });

            if (game.IsClosed)
                return Results.BadRequest(new { error = "game is closed" });

            if (game.UsesLobby && !game.HasStarted)
                return Results.BadRequest(new { error = "game has not started yet" });

            if (!game.PlayerIndexById.TryGetValue(playerId, out playerIndex))
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

            // Détecter si c'est le premier coup (aucune tuile sur le plateau)
            isFirstMove = !currentState.Board.Grid.Cast<object?>().Any(t => t is not null);
        }

        var currentPlayer  = currentState.Players[playerIndex];

        // Paramètres depuis payload
        var topPerCategory = 2;
        if (payload is { } p && p.TryGetProperty("top", out var topProp) && topProp.TryGetInt32(out var t))
            topPerCategory = Math.Clamp(t, 1, 10);

        // Déléguer le calcul à Lama.AIServer (isolation CPU)
        var suggestions = await aiClient.SuggestAsync(
            currentPlayer.Rack,
            currentState.Board,
            isFirstMove,
            topPerCategory,
            timeoutSeconds: 15,
            ct: CancellationToken.None);

        var mapped = suggestions
            .Select(s => MapAISuggestion(s))
            .ToList();

        return Results.Ok(new
        {
            gameId,
            suggestions = mapped,
            message = mapped.Count > 0
                ? $"{mapped.Count} suggestion(s) trouvées."
                : "Aucune suggestion disponible."
        });
    }

    private static object MapAISuggestion(AISuggestion s)
    {
        var col       = (char)('A' + s.StartCol);
        var row       = s.StartRow + 1;
        var position  = $"{col}{row}";
        var direction = s.IsHorizontal ? "H" : "V";

        return new
        {
            word      = s.Word,
            position,
            direction,
            score     = s.Score,
            length    = s.Length,
            category  = s.Category
        };
    }

    private static string GenerateRandomGameName() => $"lama-{Guid.NewGuid().ToString("N")[..8]}";

    private static string ComputePasswordHash(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool VerifyPassword(string password, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
            return false;

        return string.Equals(ComputePasswordHash(password), expectedHash, StringComparison.Ordinal);
    }
}

