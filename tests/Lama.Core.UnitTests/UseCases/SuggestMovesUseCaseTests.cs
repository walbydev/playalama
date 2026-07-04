using FluentAssertions;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.UseCases;

public class SuggestMovesUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UnknownGameId_ThrowsGameException()
    {
        var (_, _, _, createUc, _, _, _, _, _) = await GameFixture.CreateReadyGame();
        var suggestUc = new SuggestMovesUseCase(createUc);

        var act = async () => await suggestUc.ExecuteAsync(
            new SuggestMovesRequest("unknown-id", "p1"));

        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task ExecuteAsync_NotCurrentPlayer_ThrowsGameException()
    {
        var (gameId, aliceId, bobId, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();
        var suggestUc = new SuggestMovesUseCase(createUc);

        // Bob tries to get suggestions when it's Alice's turn
        var act = async () => await suggestUc.ExecuteAsync(
            new SuggestMovesRequest(gameId, bobId));

        await act.Should().ThrowAsync<GameException>(
            because: "Bob cannot get suggestions when it's Alice's turn");
    }

    [Fact]
    public async Task ExecuteAsync_CurrentPlayer_ReturnsSuggestions()
    {
        var (gameId, aliceId, _, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();

        // Force rack with letters that can form words in the test dictionary
        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'A', 'S', 'I', 'T']);
        var suggestUc = new SuggestMovesUseCase(createUc);

        var response = await suggestUc.ExecuteAsync(
            new SuggestMovesRequest(gameId, aliceId));

        response.Suggestions.Should().NotBeNull();
        response.Suggestions.Should().NotBeEmpty(
            because: "the rack LAMASIT should produce at least one valid suggestion from the dictionary");
    }

    [Fact]
    public async Task ExecuteAsync_MarksSessionAsSuggestionsUsed()
    {
        var (gameId, aliceId, _, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();
        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'A', 'S', 'I', 'T']);
        var suggestUc = new SuggestMovesUseCase(createUc);

        await suggestUc.ExecuteAsync(new SuggestMovesRequest(gameId, aliceId));

        var session = createUc.GetSession(gameId);
        session!.SuggestionsUsed.Should().BeTrue(
            because: "calling suggest should mark the session as having used suggestions");
    }

    [Fact]
    public async Task ExecuteAsync_WithTop_LimitsNumberOfSuggestions()
    {
        var (gameId, aliceId, _, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();
        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'A', 'S', 'I', 'T']);
        var suggestUc = new SuggestMovesUseCase(createUc);

        var response = await suggestUc.ExecuteAsync(
            new SuggestMovesRequest(gameId, aliceId, Top: 1));

        response.Suggestions.Should().HaveCountLessOrEqualTo(1,
            because: "Top=1 should limit suggestions to at most 1");
    }

    [Fact]
    public async Task ExecuteAsync_WithSortLength_UsesLengthStrategy()
    {
        var (gameId, aliceId, _, createUc, _, _, _, _, _) =
            await GameFixture.CreateReadyGame();
        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'A', 'S', 'I', 'T']);
        var suggestUc = new SuggestMovesUseCase(createUc);

        var response = await suggestUc.ExecuteAsync(
            new SuggestMovesRequest(gameId, aliceId, Sort: MoveSuggestionSort.Length));

        response.Suggestions.Should().NotBeNull();
    }
}
