using Lama.Contracts;
using Lama.Domain.Rating;
using Lama.Server.Data;
using Lama.Server.Data.Models.Rating;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Services;

/// <summary>
/// Implémentation EF Core de <see cref="IPlayerRatingService"/>.
/// Persiste les ratings Elo dans <c>rating.player_ratings</c>.
/// </summary>
public sealed class PlayerRatingService(LamaDbContext db) : IPlayerRatingService
{
    private static readonly EloCalculator EloCalc = new();
    private static readonly LevelDeterminer LevelDet = new();

    /// <inheritdoc/>
    public async Task<PlayerRating> GetRatingAsync(string playerId)
    {
        if (!Guid.TryParse(playerId, out var guid))
            return DefaultRating(playerId);

        var rows = await db.PlayerRatings
            .Where(r => r.PlayerId == guid)
            .ToListAsync();

        if (rows.Count == 0)
            return DefaultRating(playerId);

        var open       = rows.FirstOrDefault(r => r.Queue == "open");
        var tournament = rows.FirstOrDefault(r => r.Queue == "tournament");

        var eloOpen       = (double)(open?.EloRating       ?? 1200m);
        var eloTournament = (double)(tournament?.EloRating  ?? 1200m);
        var combined      = open ?? tournament;

        var (levelEnum, levelName, _) = LevelDet.DetermineLevel(eloOpen);

        return new PlayerRating(
            PlayerId:      playerId,
            EloRating:     eloOpen,
            Level:         (int)levelEnum,
            LevelName:     levelName,
            EloOpen:       eloOpen,
            EloTournament: eloTournament,
            WinsCount:     combined?.GamesWon       ?? 0,
            LossesCount:   combined?.GamesLost      ?? 0,
            AbandonedCount:combined?.GamesAbandoned ?? 0,
            AverageScore:  (double)(combined?.AvgScore ?? 0m),
            LastGameAt:    combined?.LastGameDate,
            UpdatedAt:     combined?.UpdatedAt ?? DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task UpdateRatingsAsync(IReadOnlyList<GameResult> gameResults)
    {
        if (gameResults.Count == 0) return;

        var queueStr = gameResults[0].Queue switch
        {
            RankingQueue.Tournament     => "tournament",
            RankingQueue.CasualUnranked => "casual",
            _                          => "open"
        };

        // ── Ensure PlayerEntity rows exist ───────────────────────────────────
        foreach (var result in gameResults)
        {
            if (!Guid.TryParse(result.PlayerId, out var pid)) continue;

            if (!await db.Players.AnyAsync(p => p.PlayerId == pid))
            {
                db.Players.Add(new PlayerEntity
                {
                    PlayerId  = pid,
                    Username  = result.PlayerName,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();

        // ── Load or create PlayerRatingEntity rows ───────────────────────────
        var playerGuids = gameResults
            .Select(r => Guid.TryParse(r.PlayerId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var existingRatings = await db.PlayerRatings
            .Where(r => playerGuids.Contains(r.PlayerId) && r.Queue == queueStr)
            .ToListAsync();

        // ── Apply Elo changes ────────────────────────────────────────────────
        foreach (var result in gameResults)
        {
            if (!Guid.TryParse(result.PlayerId, out var pid)) continue;

            var row = existingRatings.FirstOrDefault(r => r.PlayerId == pid);

            if (row is null)
            {
                row = new PlayerRatingEntity
                {
                    PlayerId  = pid,
                    Queue     = queueStr,
                    EloRating = (decimal)result.OpponentRatings
                        .DefaultIfEmpty(EloCalculator.InitialRating)
                        .Average(),
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.PlayerRatings.Add(row);
                existingRatings.Add(row);
            }

            var change = EloCalc.CalculateRatingChange(
                (double)row.EloRating,
                result.OpponentRatings,
                result.Rank,
                gameResults.Count);

            // L'Elo n'est approvisionné que si la partie n'est pas un abandon et
            // n'a pas utilisé de suggestion.
            // Note : play.suggest est interdit en mode Tournament, donc SuggestionsUsed
            // sera toujours false en Tournament — la condition est uniforme.
            var eloFeeds = result.IsRanked
                && result.Queue != RankingQueue.CasualUnranked
                && !result.IsAbandoned
                && !result.SuggestionsUsed;

            if (eloFeeds)
                row.EloRating = (decimal)EloCalc.ApplyRatingChange((double)row.EloRating, change);

            row.GamesPlayed++;

            if (result.IsAbandoned)
                row.GamesAbandoned++;
            else if (result.Rank == 1)
                row.GamesWon++;
            else
                row.GamesLost++;

            row.TotalPoints += result.Score;
            row.AvgScore     = row.GamesPlayed > 0
                ? (decimal)row.TotalPoints / row.GamesPlayed
                : 0;
            row.LastGameDate = result.PlayedAt;
            row.UpdatedAt    = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
        RankingQueue queue = RankingQueue.GlobalPrestige,
        int topCount = 100)
    {
        var queueStr = queue switch
        {
            RankingQueue.Tournament => "tournament",
            _                      => "open"
        };

        var rows = await db.PlayerRatings
            .Where(r => r.Queue == queueStr)
            .Where(r => r.Player != null && r.Player.Username != "root")
            .OrderByDescending(r => r.EloRating)
            .Take(topCount)
            .Include(r => r.Player)
            .ToListAsync();

        return rows.Select(r =>
        {
            var elo = (double)r.EloRating;
            var (lvl, name, _) = LevelDet.DetermineLevel(elo);
            return new PlayerRating(
                PlayerId:      r.PlayerId.ToString("N"),
                EloRating:     elo,
                Level:         (int)lvl,
                LevelName:     name,
                EloOpen:       elo,
                WinsCount:     r.GamesWon,
                LossesCount:   r.GamesLost,
                AbandonedCount:r.GamesAbandoned,
                AverageScore:  (double)(r.AvgScore ?? 0m),
                LastGameAt:    r.LastGameDate,
                UpdatedAt:     r.UpdatedAt);
        }).ToList();
    }

    /// <inheritdoc/>
    public Task<PlayerStatistics> GetStatisticsAsync(string playerId) =>
        Task.FromResult(new PlayerStatistics(
            playerId,
            All: new PeriodStats(0, 0, 0, 0, 0),
            Last7Days: new PeriodStats(0, 0, 0, 0, 0),
            Last30Days: new PeriodStats(0, 0, 0, 0, 0),
            Last365Days: new PeriodStats(0, 0, 0, 0, 0)));

    /// <inheritdoc/>
    public Task<IReadOnlyList<PlayerRating>> GetPlayersByLevelAsync(int level) =>
        Task.FromResult<IReadOnlyList<PlayerRating>>([]);

    /// <inheritdoc/>
    public Task ResetRatingsAsync() => Task.CompletedTask;

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PlayerRating DefaultRating(string playerId)
    {
        var (lvl, name, _) = LevelDet.DetermineLevel(EloCalculator.InitialRating);
        return new PlayerRating(
            PlayerId:  playerId,
            EloRating: EloCalculator.InitialRating,
            Level:     (int)lvl,
            LevelName: name);
    }
}
