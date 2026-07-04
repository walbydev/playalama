using Lama.Contracts;
using Lama.Domain.Rating;
using Lama.Server.Data;
using Lama.Server.Data.Models.Rating;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Endpoints.Players;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record PlayerRatingResponse(
    string PlayerId,
    string Username,
    string LevelName,
    int Level,
    int EloOpen,
    int EloTournament,
    double GlobalPrestige,
    int Wins,
    int Losses,
    int Abandoned,
    double WinRate,
    double AverageScore,
    DateTimeOffset? LastGameAt);

public sealed record LeaderboardEntryResponse(
    string PlayerId,
    string Username,
    string? CountryCode,
    string LevelName,
    int Level,
    int Elo,
    int Wins,
    int Games);

// ── Endpoints ─────────────────────────────────────────────────────────────────

/// <summary>
/// Endpoints de classement et de rating.
/// </summary>
public static class RatingEndpoints
{
    private static readonly LevelDeterminer LevelDet = new();

    public static void MapRatingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1")
            .WithTags("Rating");

        group.MapGet("/leaderboard", GetLeaderboard())
            .WithName("GetLeaderboard")
            .WithDescription("Classement global des joueurs, filtrable par file (open/tournament).")
            .Produces<IReadOnlyList<LeaderboardEntryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/players/me/rating", GetMyRating())
            .WithName("GetMyRating")
            .WithDescription("Rating et niveau du joueur authentifié.")
            .Produces<PlayerRatingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/players/{playerId}/rating", GetPlayerRating())
            .WithName("GetPlayerRating")
            .WithDescription("Rating et niveau d'un joueur par son identifiant.")
            .Produces<PlayerRatingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ── GET /api/v1/leaderboard ───────────────────────────────────────────────

    private static Func<HttpRequest, LamaDbContext, Task<IResult>> GetLeaderboard()
    {
        return async (request, db) =>
        {
            var mode  = request.Query["mode"].ToString();
            var queue = string.Equals(mode, "tournament", StringComparison.OrdinalIgnoreCase)
                ? "tournament"
                : "open";

            if (!int.TryParse(request.Query["limit"], out var limit) || limit <= 0 || limit > 200)
                limit = 50;

            try
            {
                var rows = await db.PlayerRatings
                    .Where(r => r.Queue == queue)
                    .Where(r => r.Player != null && r.Player.Username != "root")
                    .OrderByDescending(r => r.EloRating)
                    .Take(limit)
                    .Include(r => r.Player)
                    .ToListAsync();

                var entries = rows
                    .Where(r => r.Player is not null)
                    .Select(r =>
                    {
                        var elo = (double)r.EloRating;
                        var (_, levelName, _) = LevelDet.DetermineLevel(elo);
                        var level = LevelDet.DetermineLevel(elo).level;
                        return new LeaderboardEntryResponse(
                            PlayerId:  r.PlayerId.ToString("N"),
                            Username:  r.Player!.Username,
                            CountryCode: r.Player.CountryCode,
                            LevelName: levelName,
                            Level:     (int)level,
                            Elo:       (int)r.EloRating,
                            Wins:      r.GamesWon,
                            Games:     r.GamesPlayed);
                    })
                    .ToList();

                return Results.Ok(entries);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Ok(Array.Empty<LeaderboardEntryResponse>());
            }
        };
    }

    // ── GET /api/v1/players/me/rating ─────────────────────────────────────────

    private static Func<HttpContext, LamaDbContext, Task<IResult>> GetMyRating()
    {
        return async (context, db) =>
        {
            var raw = context.User?.FindFirst("playerId")?.Value;
            if (raw is null || !Guid.TryParse(raw, out var playerId))
                return Results.Unauthorized();

            return await BuildRatingResponse(db, playerId);
        };
    }

    // ── GET /api/v1/players/{playerId}/rating ─────────────────────────────────

    private static Func<string, LamaDbContext, Task<IResult>> GetPlayerRating()
    {
        return async (playerId, db) =>
        {
            if (!Guid.TryParse(playerId, out var guid))
                return Results.NotFound(new { error = "Identifiant invalide." });

            return await BuildRatingResponse(db, guid);
        };
    }

    // ── Shared helper ─────────────────────────────────────────────────────────

    private static async Task<IResult> BuildRatingResponse(LamaDbContext db, Guid playerId)
    {
        try
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.PlayerId == playerId);
            if (player is null)
                return Results.NotFound(new { error = "Joueur introuvable." });

            var rows = await db.PlayerRatings
                .Where(r => r.PlayerId == playerId)
                .ToListAsync();

            var open       = rows.FirstOrDefault(r => r.Queue == "open");
            var tournament = rows.FirstOrDefault(r => r.Queue == "tournament");

            var eloOpen       = (double)(open?.EloRating       ?? 1200m);
            var eloTournament = (double)(tournament?.EloRating  ?? 1200m);
            var globalPrestige = eloTournament * 0.7 + eloOpen * 0.3;

            var (levelEnum, levelName, _) = LevelDet.DetermineLevel(eloOpen);

            var combined = open ?? tournament;
            var wins      = combined?.GamesWon       ?? 0;
            var losses    = combined?.GamesLost      ?? 0;
            var abandoned = combined?.GamesAbandoned ?? 0;
            var winRate   = wins + losses > 0 ? (double)wins / (wins + losses) * 100 : 0;
            var avgScore  = (double)(combined?.AvgScore ?? 0m);

            return Results.Ok(new PlayerRatingResponse(
                PlayerId:       playerId.ToString("N"),
                Username:       player.Username,
                LevelName:      levelName,
                Level:          (int)levelEnum,
                EloOpen:        (int)eloOpen,
                EloTournament:  (int)eloTournament,
                GlobalPrestige: Math.Round(globalPrestige, 1),
                Wins:           wins,
                Losses:         losses,
                Abandoned:      abandoned,
                WinRate:        Math.Round(winRate, 1),
                AverageScore:   Math.Round(avgScore, 1),
                LastGameAt:     combined?.LastGameDate));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Json(new { error = "Service de rating temporairement indisponible." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
