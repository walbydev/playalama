using Lama.Server.Data;
using Lama.Server.Contracts.Api;
using Lama.Server.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lama.Server.Endpoints;

public static class GamesReadEndpoints
{
    public static IEndpointRouteBuilder MapGamesReadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games", GetGamesAsync);
        app.MapGet("/api/games/{gameId}", GetGameByIdAsync);
        return app;
    }

    private static async Task<IResult> GetGamesAsync(GameHubState state, LamaDbContext db, CancellationToken cancellationToken)
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
        catch (PostgresException ex) when (GamesEndpointParsers.IsMissingDatabaseObject(ex))
        {
            // Backward compatibility: DB may still be at the minimal sessions.games schema.
        }

        foreach (var persistedGame in persistedGames)
        {
            var gameId = persistedGame.GameId.ToString("N");
            if (merged.ContainsKey(gameId))
                continue;

            var parsedLevel = GamesEndpointParsers.ParseGameLevelToken(persistedGame.GameLevel);
            var parsedQueue = GamesEndpointParsers.ParseRankingQueueToken(persistedGame.Queue);
            var normalizedStatus = GamesEndpointParsers.NormalizeStatusToken(persistedGame.Status);
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
    }

    private static async Task<IResult> GetGameByIdAsync(string gameId, GameHubState state, LamaDbContext db, CancellationToken cancellationToken)
    {
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
                    board = GamesEndpointParsers.CaptureBoard(stateSnapshot.Board),
                    moves = game.Moves,
                    source = "memory"
                });
            }
        }

        if (!Guid.TryParse(gameId, out var gameGuid))
            return Results.NotFound(new { error = "game not found" });

        var persistedGame = await db.SessionGames
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameId == gameGuid, cancellationToken);

        if (persistedGame is null)
            return Results.NotFound(new { error = "game not found" });

        var parsedLevel = GamesEndpointParsers.ParseGameLevelToken(persistedGame.GameLevel);
        var parsedQueue = GamesEndpointParsers.ParseRankingQueueToken(persistedGame.Queue);
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
                    var payload = GamesEndpointParsers.ParseActionPayload(x.ActionPayload);
                    playersBySessionId.TryGetValue(x.PlayerSessionId, out var owner);

                    return (object)new
                    {
                        MoveId = x.TurnId.ToString("N"),
                        PlayerId = owner?.PlayerId.ToString("N") ?? x.PlayerSessionId.ToString("N"),
                        PlayerName = owner?.Nickname ?? "unknown",
                        Command = GamesEndpointParsers.ToOnlineCommand(x.ActionType),
                        Payload = payload,
                        PlayedAt = x.ExecutedAt,
                        Score = GamesEndpointParsers.ExtractScoreFromPayload(payload),
                        TurnNumber = x.TurnNumber,
                        Placements = GamesEndpointParsers.ExtractPlacementsFromPayload(payload)
                    };
                })
                .ToList();

            var dbBoardState = await db.SessionBoardStates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.GameId == gameGuid, cancellationToken);

            if (dbBoardState is not null)
                persistedBoard = GamesEndpointParsers.ParseBoardTilesFromJson(dbBoardState.BoardJson);
        }
        catch (PostgresException ex) when (GamesEndpointParsers.IsMissingDatabaseObject(ex))
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
    }
}
