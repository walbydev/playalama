using Lama.Server.Data;
using Lama.Server.Runtime;
using Lama.Server.Security;
using Lama.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lama.Server.Endpoints.Players;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record PlayerProfileResponse(
    string PlayerId,
    string Username,
    string? Email,
    string? CountryCode,
    DateTimeOffset CreatedAt);

public sealed record UpdateProfileRequest(string? Email, string? NewPassword, string? CurrentPassword, string? CountryCode);

public sealed record PlayerGameHistoryItem(
    string GameId,
    string GameLevel,
    string Queue,
    string Status,
    DateTimeOffset EndedAt,
    int DurationSeconds,
    bool IsWinner);

// ── Endpoints ─────────────────────────────────────────────────────────────────

/// <summary>
/// Endpoints de gestion du profil joueur (nécessitent un JWT valide).
/// </summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/players")
            .WithTags("Players");

        group.MapGet("/me", GetProfile())
            .WithName("GetMyProfile")
            .WithDescription("Retourne le profil du joueur connecté.")
            .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/me", UpdateProfile())
            .WithName("UpdateMyProfile")
            .WithDescription("Met à jour email et/ou mot de passe.")
            .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me/games", GetMyGames())
            .WithName("GetMyGames")
            .WithDescription("Retourne l'historique de parties du joueur connecté.")
            .Produces<List<PlayerGameHistoryItem>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static Func<HttpContext, LamaDbContext, Task<IResult>> GetProfile()
    {
        return async (context, db) =>
        {
            var playerId = ExtractPlayerId(context);
            if (playerId is null)
                return Results.Unauthorized();

            try
            {
                var player = await db.Players.FirstOrDefaultAsync(p => p.PlayerId == playerId);
                if (player is null)
                    return Results.Unauthorized();

                return Results.Ok(new PlayerProfileResponse(
                    player.PlayerId.ToString("N"),
                    player.Username,
                    player.Email,
                    player.CountryCode,
                    player.CreatedAt));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new { error = "Service de profil temporairement indisponible." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        };
    }

    private static Func<HttpContext, UpdateProfileRequest, LamaDbContext, Task<IResult>> UpdateProfile()
    {
        return async (context, request, db) =>
        {
            var playerId = ExtractPlayerId(context);
            if (playerId is null)
                return Results.Unauthorized();

            try
            {
                var player = await db.Players.FirstOrDefaultAsync(p => p.PlayerId == playerId);
                if (player is null)
                    return Results.Unauthorized();

                // Mise à jour email (optionnel)
                if (request.Email is not null)
                {
                    player.Email = string.IsNullOrWhiteSpace(request.Email)
                        ? null
                        : request.Email.Trim().ToLowerInvariant();
                }

                // Mise à jour pays (optionnel)
                if (request.CountryCode is not null)
                {
                    player.CountryCode = string.IsNullOrWhiteSpace(request.CountryCode)
                        ? null
                        : request.CountryCode.Trim().ToUpperInvariant()[..Math.Min(2, request.CountryCode.Trim().Length)];
                }

                // Mise à jour mot de passe (nécessite l'ancien si compte avec mot de passe)
                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    if (request.NewPassword.Length < 6)
                        return Results.BadRequest(new { error = "Le nouveau mot de passe doit contenir au moins 6 caractères." });

                    if (player.PasswordHash is not null)
                    {
                        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                            !PasswordHasher.Verify(request.CurrentPassword, player.PasswordHash))
                            return Results.BadRequest(new { error = "Mot de passe actuel incorrect." });
                    }

                    player.PasswordHash = PasswordHasher.Hash(request.NewPassword);
                }

                await db.SaveChangesAsync();

                return Results.Ok(new PlayerProfileResponse(
                    player.PlayerId.ToString("N"),
                    player.Username,
                    player.Email,
                    player.CountryCode,
                    player.CreatedAt));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new { error = "Service de profil temporairement indisponible." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        };
    }

    private static Func<HttpContext, LamaDbContext, GameHubState, Task<IResult>> GetMyGames()
    {
        return async (context, db, state) =>
        {
            var playerId = ExtractPlayerId(context);
            if (playerId is null)
                return Results.Unauthorized();

            try
            {
                var playerKey = playerId.Value.ToString("N");
                var mergedGames = new Dictionary<Guid, PlayerGameHistoryItem>();

                foreach (var game in state.ListGames())
                {
                    if (!Guid.TryParse(game.Id, out var gameGuid))
                        continue;

                    lock (game)
                    {
                        if (!game.PlayerIndexById.TryGetValue(playerKey, out var playerIndex))
                            continue;

                        var snapshot = game.Engine.GetGameState();
                        var participant = snapshot.Players.ElementAtOrDefault(playerIndex);
                        if (participant is null)
                            continue;

                        var isActive = !game.IsClosed && !snapshot.IsGameOver;
                        var playerScore = participant.Score;
                        var topScore = snapshot.Players.Count == 0 ? 0 : snapshot.Players.Max(p => p.Score);
                        var hasSingleWinner = snapshot.Players.Count(p => p.Score == topScore) == 1;
                        var isWinner = snapshot.IsGameOver && hasSingleWinner && playerScore == topScore;
                        var hasAbandoned = game.AbandonedPlayerIds.Contains(playerKey);

                        var status = hasAbandoned
                            ? "abandoned"
                            : isActive
                                ? (game.UsesLobby && !game.HasStarted ? "waiting" : "active")
                                : string.Equals(game.EndReason, "abandoned", StringComparison.OrdinalIgnoreCase)
                                    ? "abandoned"
                                    : "ended";

                        var queueToken = game.Queue switch
                        {
                            RankingQueue.CasualUnranked => "casual",
                            RankingQueue.Tournament => "tournament",
                            RankingQueue.GlobalPrestige => "global",
                            _ => "open"
                        };

                        var historyTimestamp = isActive ? DateTimeOffset.UtcNow : game.UpdatedAt;
                        var durationSeconds = Math.Max(0, (int)((historyTimestamp - game.CreatedAt).TotalSeconds));

                        mergedGames[gameGuid] = new PlayerGameHistoryItem(
                            gameGuid.ToString("N"),
                            game.GameLevel.ToString(),
                            queueToken,
                            status,
                            historyTimestamp,
                            durationSeconds,
                            isWinner);
                    }
                }

                // Historique terminé depuis CompletedGames via SessionPlayerInGame
                var rows = await db.SessionPlayersInGame
                    .Where(p => p.PlayerId == playerId.Value)
                    .Join(db.CompletedGames,
                        p => p.GameId,
                        g => g.GameId,
                        (p, g) => new
                        {
                            g.GameId,
                            g.GameLevel,
                            g.Queue,
                            g.Status,
                            g.EndedAt,
                            g.DurationSeconds,
                            IsWinner = g.WinningPlayerId.HasValue && g.WinningPlayerId.Value == p.PlayerId
                        })
                    .OrderByDescending(x => x.EndedAt)
                    .Take(50)
                    .ToListAsync();

                foreach (var x in rows)
                {
                    mergedGames[x.GameId] = new PlayerGameHistoryItem(
                        x.GameId.ToString("N"),
                        x.GameLevel,
                        x.Queue,
                        x.Status,
                        x.EndedAt,
                        x.DurationSeconds,
                        x.IsWinner);
                }

                var games = mergedGames.Values
                    .OrderByDescending(x => x.EndedAt)
                    .Take(100)
                    .ToList();

                return Results.Ok(games);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Ok(new List<PlayerGameHistoryItem>());
            }
        };
    }

    private static Guid? ExtractPlayerId(HttpContext context)
    {
        var raw = context.User?.FindFirst("playerId")?.Value
               ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return raw is not null && Guid.TryParse(raw, out var id) ? id : null;
    }
}
