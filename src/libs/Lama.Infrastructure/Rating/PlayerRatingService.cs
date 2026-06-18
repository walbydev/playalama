using Lama.Contracts;
using Lama.Domain.Rating;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Rating;

/// <summary>
/// Service pour gérer les ratings et statistiques des joueurs.
/// Implémente <see cref="IPlayerRatingService"/>.
/// </summary>
public sealed class PlayerRatingService : IPlayerRatingService
{
    private readonly PlayerRatingRepository _ratingRepository;
    private readonly GameResultRepository _resultRepository;
    private readonly EloCalculator _eloCalculator;
    private readonly LevelDeterminer _levelDeterminer;
    private readonly ILogger<PlayerRatingService> _logger;

    public PlayerRatingService(
        PlayerRatingRepository ratingRepository,
        GameResultRepository resultRepository,
        ILogger<PlayerRatingService> logger)
    {
        _ratingRepository = ratingRepository;
        _resultRepository = resultRepository;
        _logger = logger;
        _eloCalculator = new EloCalculator();
        _levelDeterminer = new LevelDeterminer();
    }

    /// <inheritdoc />
    public Task<PlayerRating> GetRatingAsync(string playerId)
    {
        var rating = _ratingRepository.GetRating(playerId);
        return Task.FromResult(rating);
    }

    /// <inheritdoc />
    public async Task UpdateRatingsAsync(IReadOnlyList<GameResult> gameResults)
    {
        if (gameResults.Count == 0)
            return;

        _logger.LogInformation("Mise à jour des ratings pour {Count} résultats", gameResults.Count);

        var updatedRatings = new List<PlayerRating>();
        var playerIds = gameResults.Select(r => r.PlayerId).Distinct().ToList();

        // Charger les ratings actuels
        var currentRatings = _ratingRepository.GetRatings(playerIds);

        // Mettre à jour chaque joueur
        foreach (var result in gameResults)
        {
            var currentRating = currentRatings[result.PlayerId];

            var sourceElo = GetEloForQueue(currentRating, result.Queue);

            // Calculer le changement Elo
            var ratingChange = (result.IsRanked && result.Queue != RankingQueue.CasualUnranked)
                ? _eloCalculator.CalculateRatingChange(
                    sourceElo,
                    result.OpponentRatings,
                    result.Rank,
                    gameResults.Count)
                : 0;

            // Appliquer le changement
            var newQueueElo = _eloCalculator.ApplyRatingChange(sourceElo, ratingChange);
            var newOpenElo = result.Queue == RankingQueue.OpenRanked ? newQueueElo : currentRating.EloOpen;
            var newTournamentElo = result.Queue == RankingQueue.Tournament ? newQueueElo : currentRating.EloTournament;
            var compatibilityElo = newOpenElo;

            // Déterminer le nouveau niveau
            var (level, levelName, _) = _levelDeterminer.DetermineLevel(compatibilityElo);

            // Mettre à jour les statistiques
            int newWins = currentRating.WinsCount;
            int newLosses = currentRating.LossesCount;
            int newAbandoned = currentRating.AbandonedCount;
            int newStreak = currentRating.CurrentStreak;

            if (result.IsAbandoned)
            {
                newAbandoned++;
                newStreak = 0; // Abandon remet la série à zéro
            }
            else if (result.Rank == 1)
            {
                newWins++;
                newStreak = newStreak > 0 ? newStreak + 1 : 1;
            }
            else
            {
                newLosses++;
                newStreak = newStreak < 0 ? newStreak - 1 : -1;
            }

            // Calculer nouveaux scores
            var allScores = gameResults.Where(r => r.PlayerId == result.PlayerId)
                .Select(r => r.Score)
                .ToList();

            int newHighScore = Math.Max(currentRating.HighScore, result.Score);
            double newAverageScore = allScores.Count > 0
                ? (currentRating.AverageScore * currentRating.TotalGames + result.Score) / (currentRating.TotalGames + 1)
                : result.Score;

            int newHighestStreak = Math.Max(currentRating.HighestStreak, Math.Abs(newStreak));

            var updatedRating = new PlayerRating(
                PlayerId: result.PlayerId,
                EloRating: compatibilityElo,
                Level: (int)level,
                LevelName: levelName,
                EloOpen: newOpenElo,
                EloTournament: newTournamentElo,
                WinsCount: newWins,
                LossesCount: newLosses,
                AbandonedCount: newAbandoned,
                CurrentStreak: newStreak,
                HighestStreak: newHighestStreak,
                HighScore: newHighScore,
                AverageScore: newAverageScore,
                LastGameAt: result.PlayedAt,
                UpdatedAt: DateTimeOffset.UtcNow);

            updatedRatings.Add(updatedRating);

            _logger.LogDebug(
                "Rating mis à jour : {PlayerId} | Elo {Old} → {New} ({Change:+0;-0}) | {Level}",
                result.PlayerId,
                sourceElo,
                newQueueElo,
                ratingChange,
                levelName);
        }

        // Sauvegarder les ratings mis à jour
        _ratingRepository.SaveRatings(updatedRatings);

        // Sauvegarder les résultats de la partie
        _resultRepository.SaveGameResults(gameResults);
    }

    /// <inheritdoc />
    public Task<PlayerStatistics> GetStatisticsAsync(string playerId)
    {
        var results = _resultRepository.LoadPlayerResults(playerId);
        var now = DateTimeOffset.UtcNow;

        var all = CalculatePeriodStats(results);
        var last7Days = CalculatePeriodStats(results, now.AddDays(-7));
        var last30Days = CalculatePeriodStats(results, now.AddDays(-30));
        var last365Days = CalculatePeriodStats(results, now.AddDays(-365));

        var stats = new PlayerStatistics(
            PlayerId: playerId,
            All: all,
            Last7Days: last7Days,
            Last30Days: last30Days,
            Last365Days: last365Days);

        return Task.FromResult(stats);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
        RankingQueue queue = RankingQueue.GlobalPrestige,
        int topCount = 100)
    {
        var all = _ratingRepository.LoadAllRatings().Values;

        IEnumerable<PlayerRating> sorted = queue switch
        {
            RankingQueue.OpenRanked => all.OrderByDescending(r => r.EloOpen),
            RankingQueue.Tournament => all.OrderByDescending(r => r.EloTournament),
            _ => all.OrderByDescending(r => r.GlobalPrestige)
        };

        var leaderboard = sorted.Take(topCount).ToList();
        return Task.FromResult((IReadOnlyList<PlayerRating>)leaderboard);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlayerRating>> GetPlayersByLevelAsync(int level)
    {
        var players = _ratingRepository.GetPlayersByLevel(level);
        return Task.FromResult((IReadOnlyList<PlayerRating>)players);
    }

    /// <inheritdoc />
    public Task ResetRatingsAsync()
    {
        _logger.LogWarning("Réinitialisation de tous les ratings [ADMIN]");
        _ratingRepository.ClearAll();
        _resultRepository.ClearAllResults();
        return Task.CompletedTask;
    }

    private static double GetEloForQueue(PlayerRating rating, RankingQueue queue) =>
        queue switch
        {
            RankingQueue.Tournament => rating.EloTournament,
            RankingQueue.OpenRanked => rating.EloOpen,
            RankingQueue.CasualUnranked => rating.EloOpen,
            RankingQueue.GlobalPrestige => rating.GlobalPrestige,
            _ => rating.EloOpen
        };

    /// <summary>
    /// Calcule les statistiques sur une période.
    /// </summary>
    private PeriodStats CalculatePeriodStats(List<GameResult> results, DateTimeOffset? since = null)
    {
        var filtered = since.HasValue
            ? results.Where(r => r.PlayedAt >= since).ToList()
            : results;

        int wins = 0;
        int losses = 0;
        int abandoned = 0;
        int highScore = 0;
        double totalScore = 0;

        foreach (var result in filtered)
        {
            highScore = Math.Max(highScore, result.Score);
            totalScore += result.Score;

            if (result.IsAbandoned)
                abandoned++;
            else if (result.Rank == 1)
                wins++;
            else
                losses++;
        }

        var count = wins + losses;
        var averageScore = count > 0 ? totalScore / count : 0;

        return new PeriodStats(
            Wins: wins,
            Losses: losses,
            Abandoned: abandoned,
            HighScore: highScore,
            AverageScore: averageScore);
    }
}

