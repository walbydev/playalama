using FluentAssertions;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.UseCases;

public class ChallengeWordUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UnknownGameId_ThrowsGameException()
    {
        var (_, _, _, createUc, _, _, _, _, _) = await GameFixture.CreateReadyGame();
        var challengeUc = new ChallengeWordUseCase(createUc);

        var act = async () => await challengeUc.ExecuteAsync(
            new ChallengeWordRequest("unknown-id", "p1"));

        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task ExecuteAsync_NotCurrentPlayer_ThrowsGameException()
    {
        var (gameId, aliceId, bobId, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();
        var challengeUc = new ChallengeWordUseCase(createUc);

        // Bob tries to challenge before his turn (Alice starts)
        var act = async () => await challengeUc.ExecuteAsync(
            new ChallengeWordRequest(gameId, bobId));

        await act.Should().ThrowAsync<GameException>(
            because: "Bob cannot challenge when it's Alice's turn");
    }

    [Fact]
    public async Task ExecuteAsync_NoPreviousMove_ThrowsGameException()
    {
        var (gameId, aliceId, _, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();
        var challengeUc = new ChallengeWordUseCase(createUc);

        var act = async () => await challengeUc.ExecuteAsync(
            new ChallengeWordRequest(gameId, aliceId));

        await act.Should().ThrowAsync<GameException>(
            because: "no move has been played yet, so there's nothing to challenge");
    }
}
