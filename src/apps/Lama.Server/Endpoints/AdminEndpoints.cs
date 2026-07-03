using Lama.Contracts;
using Lama.Server.Contracts.Api;
using Lama.Server.Data;
using Lama.Server.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/v1/admin")
            .WithTags("Admin");

        // ── Users management ──────────────────────────────────────────────

        admin.MapGet("/users", async (
            HttpContext httpContext,
            LamaDbContext db,
            GameHubState state,
            IConfiguration config,
            string? filter,
            CancellationToken cancellationToken) =>
        {
            if (!StatusEndpoints.IsAuthorized(httpContext, config))
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

            try
            {
                var activePlayerIds = state.GetActivePlayerIds();

                var cutoff60 = DateTimeOffset.UtcNow.AddDays(-60);
                var cutoff24h = DateTimeOffset.UtcNow.AddDays(-1);

                var query = db.Players
                    .AsNoTracking()
                    .Include(p => p.Ratings)
                    .AsQueryable();

                if (string.Equals(filter, "connected", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => activePlayerIds.Contains(p.PlayerId));
                }
                else if (string.Equals(filter, "daily", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.LastLoginAt.HasValue && p.LastLoginAt >= cutoff24h);
                }
                else if (string.Equals(filter, "inactive", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => !p.LastLoginAt.HasValue || p.LastLoginAt < cutoff60);
                }

                var users = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new AdminUserDto(
                        p.PlayerId,
                        p.Username,
                        p.Email,
                        p.CountryCode,
                        p.CreatedAt,
                        p.LastLoginAt,
                        p.Ratings.FirstOrDefault(r => r.Queue == "open")!.EloRating,
                        p.Ratings.FirstOrDefault(r => r.Queue == "open")!.GamesPlayed,
                        p.Ratings.FirstOrDefault(r => r.Queue == "open")!.GamesWon,
                        activePlayerIds.Contains(p.PlayerId)))
                    .ToListAsync(cancellationToken);

                return Results.Ok(new { total = users.Count, users });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(
                    new { error = "Service de liste utilisateurs temporairement indisponible." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("AdminListUsersDetailed")
        .WithDescription("Liste détaillée des joueurs (avec lastLogin, Elo, statut connecté, filtre connected/daily/inactive).")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        admin.MapDelete("/users/{playerId:guid}", async (
            HttpContext httpContext,
            Guid playerId,
            LamaDbContext db,
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            if (!StatusEndpoints.IsAuthorized(httpContext, config))
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

            try
            {
                var player = await db.Players
                    .Include(p => p.Ratings)
                    .FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

                if (player is null)
                    return Results.NotFound(new { error = "Joueur introuvable." });

                var ratings = await db.PlayerRatings
                    .Where(r => r.PlayerId == playerId)
                    .ToListAsync(cancellationToken);
                db.PlayerRatings.RemoveRange(ratings);

                var sessionPlayers = await db.SessionPlayersInGame
                    .Where(s => s.PlayerId == playerId)
                    .ToListAsync(cancellationToken);
                db.SessionPlayersInGame.RemoveRange(sessionPlayers);

                db.Players.Remove(player);
                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(new { deleted = true, playerId });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(
                    new { error = "Erreur lors de la suppression du joueur." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("AdminDeleteUser")
        .WithDescription("Supprime un joueur et ses données associées (ratings, sessions).")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // ── Games management ──────────────────────────────────────────────

        admin.MapGet("/games", async (
            HttpContext httpContext,
            GameHubState state,
            LamaDbContext db,
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            if (!StatusEndpoints.IsAuthorized(httpContext, config))
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

            try
            {
                var inMemoryGames = state.ListGames();
                var inMemoryIds = inMemoryGames.Select(g => g.Id).ToHashSet();

                var inMemoryDtos = inMemoryGames.Select(g =>
                {
                    var gs = g.Engine.GetGameState();
                    return new AdminGameDto(
                        g.Id,
                        g.GameLevel,
                        g.Queue,
                        g.BoardSize,
                        g.RackSize,
                        g.Language,
                        g.IsClosed ? "closed" : (g.HasStarted ? "active" : "lobby"),
                        gs.IsGameOver,
                        g.Players.Count,
                        g.Moves.Count,
                        g.CreatedAt,
                        g.UpdatedAt,
                        "memory",
                        g.Players.Select(p => new AdminGamePlayerDto(p.PlayerName, p.IsBot)).ToList());
                }).ToList();

                var persistedRaw = await db.SessionGames
                    .AsNoTracking()
                    .Where(s => !inMemoryIds.Contains(s.GameId.ToString("N").ToUpperInvariant()) &&
                                !inMemoryIds.Contains(s.GameId.ToString()))
                    .OrderByDescending(s => s.UpdatedAt)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                var persistedGames = persistedRaw.Select(s => new AdminGameDto(
                        s.GameId.ToString(),
                        Enum.Parse<GameLevel>(s.GameLevel, true),
                        Enum.Parse<RankingQueue>(s.Queue, true),
                        s.BoardSize,
                        s.RackSize,
                        s.Language,
                        s.Status,
                        s.Status is "ended" or "abandoned" or "finished_normal",
                        0,
                        0,
                        s.CreatedAt,
                        s.UpdatedAt,
                        "database",
                        new List<AdminGamePlayerDto>()))
                    .ToList();

                var allGames = inMemoryDtos.Concat(persistedGames).ToList();

                return Results.Ok(new { total = allGames.Count, games = allGames });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(
                    new { error = "Service de liste parties temporairement indisponible." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("AdminListGames")
        .WithDescription("Liste toutes les parties (en mémoire + persistées).")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        admin.MapDelete("/games/{gameId}", async (
            HttpContext httpContext,
            string gameId,
            GameHubState state,
            LamaDbContext db,
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            if (!StatusEndpoints.IsAuthorized(httpContext, config))
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

            try
            {
                // Close in-memory game if exists
                var inMemory = state.ListGames().FirstOrDefault(g =>
                    string.Equals(g.Id, gameId, StringComparison.OrdinalIgnoreCase));
                if (inMemory is not null)
                {
                    lock (inMemory)
                    {
                        inMemory.Engine.EndGame();
                        inMemory.IsClosed = true;
                        inMemory.EndReason = "admin_deleted";
                        inMemory.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                    state.Publish(inMemory.Id, new ServerEvent("game.ended", new
                    {
                        gameId = inMemory.Id,
                        endedAt = DateTimeOffset.UtcNow,
                        reason = "admin_deleted",
                        scores = Array.Empty<object>(),
                        winner = (string?)null
                    }));
                }

                // Delete persisted data
                if (Guid.TryParse(gameId, out var gid))
                {
                    var sessionGame = await db.SessionGames
                        .FirstOrDefaultAsync(s => s.GameId == gid, cancellationToken);
                    if (sessionGame is not null)
                    {
                        var sessionPlayers = await db.SessionPlayersInGame
                            .Where(s => s.GameId == gid)
                            .ToListAsync(cancellationToken);
                        db.SessionPlayersInGame.RemoveRange(sessionPlayers);
                        var boardState = await db.SessionBoardStates
                            .FirstOrDefaultAsync(b => b.GameId == gid, cancellationToken);
                        if (boardState is not null)
                            db.SessionBoardStates.Remove(boardState);
                        var turnLogs = await db.SessionTurnLogs
                            .Where(t => t.GameId == gid)
                            .ToListAsync(cancellationToken);
                        db.SessionTurnLogs.RemoveRange(turnLogs);
                        db.SessionGames.Remove(sessionGame);
                    }

                    var completedGame = await db.CompletedGames
                        .FirstOrDefaultAsync(c => c.GameId == gid, cancellationToken);
                    if (completedGame is not null)
                        db.CompletedGames.Remove(completedGame);

                    await db.SaveChangesAsync(cancellationToken);
                }

                return Results.Ok(new { deleted = true, gameId });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(
                    new { error = "Erreur lors de la suppression de la partie." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("AdminDeleteGame")
        .WithDescription("Supprime une partie (mémoire + base de données).")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

public sealed record AdminUserDto(
    Guid PlayerId,
    string Username,
    string? Email,
    string? CountryCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    decimal EloRating,
    int GamesPlayed,
    int GamesWon,
    bool IsConnected);

public sealed record AdminGameDto(
    string GameId,
    GameLevel GameLevel,
    RankingQueue Queue,
    int BoardSize,
    int RackSize,
    string Language,
    string Status,
    bool IsGameOver,
    int PlayerCount,
    int MoveCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Source,
    List<AdminGamePlayerDto> Players);

public sealed record AdminGamePlayerDto(string PlayerName, bool IsBot);
