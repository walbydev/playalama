using Lama.Contracts;
using Lama.Infrastructure.Rating;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lama.Infrastructure.UnitTests.Rating;

public class PlayerRatingServiceTests
{
    private readonly Mock<ILogger<PlayerRatingService>> _loggerMock;
    private readonly Mock<ILogger<PlayerRatingRepository>> _ratingRepoLoggerMock;
    private readonly Mock<ILogger<GameResultRepository>> _resultRepoLoggerMock;
    private readonly PlayerRatingRepository _ratingRepository;
    private readonly GameResultRepository _resultRepository;
    private readonly PlayerRatingService _service;

    public PlayerRatingServiceTests()
    {
        _loggerMock = new Mock<ILogger<PlayerRatingService>>();
        _ratingRepoLoggerMock = new Mock<ILogger<PlayerRatingRepository>>();
        _resultRepoLoggerMock = new Mock<ILogger<GameResultRepository>>();

        // Utiliser un répertoire temporaire pour les tests
        var testDir = Path.Combine(Path.GetTempPath(), "lama-tests", Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", testDir);

        _ratingRepository = new PlayerRatingRepository(_ratingRepoLoggerMock.Object);
        _resultRepository = new GameResultRepository(_resultRepoLoggerMock.Object);
        _service = new PlayerRatingService(_ratingRepository, _resultRepository, _loggerMock.Object);
    }

    [Fact]
    public async Task GetRating_NewPlayer_ShouldReturnDefaultRating()
    {
        var rating = await _service.GetRatingAsync("new-player-123");

        Assert.NotNull(rating);
        Assert.Equal("new-player-123", rating.PlayerId);
        Assert.Equal(1200, rating.EloRating);
        Assert.Equal(1, rating.Level);
    }

    [Fact]
    public async Task UpdateRatings_Winner_ShouldGainElo()
    {
        var gameId = "game-1";
        var playerId = "player-1";
        var opponentId = "player-2";

        var initialRating = await _service.GetRatingAsync(playerId);
        var initialElo = initialRating.EloRating;

        var gameResults = new List<GameResult>
        {
            new GameResult(
                GameId: gameId,
                PlayerId: playerId,
                PlayerName: "Player One",
                Rank: 1, // Gagnant
                IsAbandoned: false,
                Score: 250,
                OpponentIds: new[] { opponentId },
                OpponentRatings: new[] { 1200.0 },
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: 600),
            new GameResult(
                GameId: gameId,
                PlayerId: opponentId,
                PlayerName: "Player Two",
                Rank: 2, // Deuxième
                IsAbandoned: false,
                Score: 200,
                OpponentIds: new[] { playerId },
                OpponentRatings: new[] { 1200.0 },
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: 600)
        };

        await _service.UpdateRatingsAsync(gameResults);

        var updatedRating = await _service.GetRatingAsync(playerId);

        Assert.True(updatedRating.EloRating > initialElo, "Le gagnant devrait gagner des points Elo");
        Assert.Equal(1, updatedRating.WinsCount);
        Assert.Equal(0, updatedRating.LossesCount);
    }

    [Fact]
    public async Task UpdateRatings_Loser_ShouldLoseElo()
    {
        var gameId = "game-1";
        var playerId = "player-1";
        var opponentId = "player-2";

        var initialRating = await _service.GetRatingAsync(playerId);
        var initialElo = initialRating.EloRating;

        var gameResults = new List<GameResult>
        {
            new GameResult(
                GameId: gameId,
                PlayerId: playerId,
                PlayerName: "Player One",
                Rank: 2, // Deuxième
                IsAbandoned: false,
                Score: 200,
                OpponentIds: new[] { opponentId },
                OpponentRatings: new[] { 1200.0 },
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: 600),
            new GameResult(
                GameId: gameId,
                PlayerId: opponentId,
                PlayerName: "Player Two",
                Rank: 1, // Gagnant
                IsAbandoned: false,
                Score: 250,
                OpponentIds: new[] { playerId },
                OpponentRatings: new[] { 1200.0 },
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: 600)
        };

        await _service.UpdateRatingsAsync(gameResults);

        var updatedRating = await _service.GetRatingAsync(playerId);

        Assert.True(updatedRating.EloRating < initialElo, "Le perdant devrait perdre des points Elo");
        Assert.Equal(0, updatedRating.WinsCount);
        Assert.Equal(1, updatedRating.LossesCount);
    }

    [Fact]
    public async Task UpdateRatings_Abandoned_ShouldNotAffectStreak()
    {
        var gameId = "game-1";
        var playerId = "player-1";

        var gameResults = new List<GameResult>
        {
            new GameResult(
                GameId: gameId,
                PlayerId: playerId,
                PlayerName: "Player One",
                Rank: 0,
                IsAbandoned: true,
                Score: 0,
                OpponentIds: new List<string>(),
                OpponentRatings: new List<double>(),
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: 60)
        };

        await _service.UpdateRatingsAsync(gameResults);

        var rating = await _service.GetRatingAsync(playerId);

        Assert.Equal(0, rating.CurrentStreak);
        Assert.Equal(1, rating.AbandonedCount);
        Assert.Equal(0, rating.WinsCount);
    }

    [Fact]
    public async Task GetLeaderboard_ShouldReturnSortedByElo()
    {
        var playerId1 = "player-1";
        var playerId2 = "player-2";

        // Mettre en cache des ratings avec Elo différents
        _ratingRepository.SaveRatings(new[]
        {
            new PlayerRating(playerId1, 1500, 3, "🎋 Lama Maître", WinsCount: 10),
            new PlayerRating(playerId2, 1200, 1, "🌱 Jeune Lama", WinsCount: 2)
        });

        var leaderboard = await _service.GetLeaderboardAsync(10);

        Assert.NotEmpty(leaderboard);
        Assert.True(leaderboard[0].EloRating >= leaderboard[1].EloRating,
            "Le classement devrait être ordonné par Elo décroissant");
    }

    [Fact]
    public async Task GetStatistics_ShouldCalculatePeriodStats()
    {
        var playerId = "player-stats";

        // Ajouter quelques résultats
        var gameResults = new List<GameResult>
        {
            new GameResult(
                GameId: "game-1",
                PlayerId: playerId,
                PlayerName: "Stats Player",
                Rank: 1,
                IsAbandoned: false,
                Score: 500,
                OpponentIds: new[] { "opponent-1" },
                OpponentRatings: new[] { 1200.0 },
                PlayedAt: DateTimeOffset.UtcNow.AddDays(-1),
                DurationSeconds: 600),
            new GameResult(
                GameId: "game-2",
                PlayerId: playerId,
                PlayerName: "Stats Player",
                Rank: 2,
                IsAbandoned: false,
                Score: 300,
                OpponentIds: new[] { "opponent-2" },
                OpponentRatings: new[] { 1200.0 },
                PlayedAt: DateTimeOffset.UtcNow,
                DurationSeconds: 600)
        };

        await _service.UpdateRatingsAsync(gameResults);
        var stats = await _service.GetStatisticsAsync(playerId);

        Assert.NotNull(stats);
        Assert.Equal(2, stats.All.TotalGames);
        Assert.Equal(50, stats.All.WinRate); // 1 victoire pour 2 parties
    }
}

