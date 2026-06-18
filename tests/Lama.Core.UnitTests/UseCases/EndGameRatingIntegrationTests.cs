using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.UseCases;

public class EndGameRatingIntegrationTests
{
    [Fact]
    public async Task EndGame_WithRatingService_UpdatesGlobalRatingsWithPlayersAndRanks()
    {
        var createUc = new CreateGameUseCase(
            GameFixture.Dictionary,
            GameFixture.LetterScores,
            GameFixture.Distribution,
            new InMemoryGameRepository());

        var joinUc = new JoinGameUseCase(createUc);
        var playUc = new PlayMoveUseCase(createUc);
        var ratingService = new CapturingRatingService();
        var endUc = new EndGameUseCase(createUc, ratingService);

        var created = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));
        var joined = await joinUc.ExecuteAsync(new JoinGameRequest(created.GameId, "Bob"));

        var engine = createUc.GetEngine(created.GameId)!;
        var aliceIndex = createUc.GetPlayerIndex(created.GameId, created.HostPlayerId);
        engine.ForceRackForTest(aliceIndex, ['L', 'A', 'M', 'A', 'M', 'A', 'L']);

        await playUc.ExecuteAsync(new PlayMoveRequest(
            created.GameId,
            created.HostPlayerId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            }));

        await endUc.ExecuteAsync(new EndGameRequest(created.GameId));

        ratingService.LastUpdatedResults.Should().NotBeNull();
        ratingService.LastUpdatedResults!.Should().HaveCount(2);

        var updatedResults = ratingService.LastUpdatedResults!;

        var alice = updatedResults.Single(r => r.PlayerId == created.HostPlayerId);
        var bob = updatedResults.Single(r => r.PlayerId == joined.PlayerId);

        alice.Rank.Should().Be(1);
        bob.Rank.Should().Be(2);
        alice.OpponentIds.Should().ContainSingle().Which.Should().Be(joined.PlayerId);
        bob.OpponentIds.Should().ContainSingle().Which.Should().Be(created.HostPlayerId);
    }

    private sealed class CapturingRatingService : IPlayerRatingService
    {
        public IReadOnlyList<GameResult>? LastUpdatedResults { get; private set; }

        public Task<PlayerRating> GetRatingAsync(string playerId)
        {
            var defaultRating = new PlayerRating(
                PlayerId: playerId,
                EloRating: 1200,
                Level: 1,
                LevelName: "Jeune Lama",
                UpdatedAt: DateTimeOffset.UtcNow);

            return Task.FromResult(defaultRating);
        }

        public Task UpdateRatingsAsync(IReadOnlyList<GameResult> gameResults)
        {
            LastUpdatedResults = gameResults;
            return Task.CompletedTask;
        }

        public Task<PlayerStatistics> GetStatisticsAsync(string playerId)
        {
            var empty = new PeriodStats(0, 0, 0, 0, 0);
            return Task.FromResult(new PlayerStatistics(playerId, empty, empty, empty, empty));
        }

        public Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
            RankingQueue queue = RankingQueue.GlobalPrestige,
            int topCount = 100) =>
            Task.FromResult((IReadOnlyList<PlayerRating>)Array.Empty<PlayerRating>());

        public Task<IReadOnlyList<PlayerRating>> GetPlayersByLevelAsync(int level) =>
            Task.FromResult((IReadOnlyList<PlayerRating>)Array.Empty<PlayerRating>());

        public Task ResetRatingsAsync() => Task.CompletedTask;
    }
}

