using FluentAssertions;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.UseCases;

/// <summary>
/// Tests unitaires pour <see cref="PassTurnUseCase"/>,
/// <see cref="SwapLettersUseCase"/> et <see cref="EndGameUseCase"/>.
/// </summary>
public class PassTurnSwapEndUseCaseTests
{
    #region PassTurnUseCase

    [Fact]
    public async Task PassTurn_AdvancesToNextPlayer()
    {
        var (gameId, aliceId, _, _, _, _, passUc, _, _) =
            await GameFixture.CreateReadyGame();

        var state = await passUc.ExecuteAsync(new PassTurnRequest(gameId, aliceId));

        state.CurrentPlayerIndex.Should().Be(1,
            because: "après que Alice passe, c'est au tour de Bob");
    }

    [Fact]
    public async Task PassTurn_DoesNotModifyBoard()
    {
        var (gameId, aliceId, _, _, _, _, passUc, _, _) =
            await GameFixture.CreateReadyGame();

        var state = await passUc.ExecuteAsync(new PassTurnRequest(gameId, aliceId));

        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
                state.Board.Grid[r, c].Should().BeNull(
                    because: "passer son tour ne modifie pas le plateau");
    }

    [Fact]
    public async Task PassTurn_DoesNotChangeScore()
    {
        var (gameId, aliceId, _, _, _, _, passUc, _, _) =
            await GameFixture.CreateReadyGame();

        var state = await passUc.ExecuteAsync(new PassTurnRequest(gameId, aliceId));

        state.Players[0].Score.Should().Be(0,
            because: "passer son tour ne rapporte aucun point");
    }

    [Fact]
    public async Task PassTurn_UnknownGame_ThrowsGameException()
    {
        var (_, _, _, createUc, _, _, _, _, _) = await GameFixture.CreateReadyGame();
        var passUc = new PassTurnUseCase(createUc);

        var act = async () => await passUc.ExecuteAsync(
            new PassTurnRequest("id-inconnu", "player-id"));

        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task PassTurn_NotCurrentPlayer_ThrowsGameException()
    {
        var (gameId, aliceId, bobId, _, _, _, passUc, _, _) =
            await GameFixture.CreateReadyGame();

        // Bob essaie de passer avant Alice
        var act = async () => await passUc.ExecuteAsync(
            new PassTurnRequest(gameId, bobId));

        await act.Should().ThrowAsync<GameException>(
            because: "Bob ne peut pas passer si ce n'est pas son tour");
    }

    #endregion

    #region SwapLettersUseCase

    [Fact]
    public async Task SwapLetters_ReturnsNewRack_SameSize()
    {
        var (gameId, aliceId, _, createUc, _, _, _, swapUc, _) =
            await GameFixture.CreateReadyGame();

        var engine = createUc.GetEngine(gameId)!;
        engine.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var request = new SwapLettersRequest(gameId, aliceId, ['L', 'A']);

        var response = await swapUc.ExecuteAsync(request);

        response.NewRack.Should().HaveCount(7,
            because: "après un échange, le rack reste à 7 lettres");
    }

    [Fact]
    public async Task SwapLetters_AdvancesToNextPlayer()
    {
        var (gameId, aliceId, _, createUc, _, _, _, swapUc, _) =
            await GameFixture.CreateReadyGame();

        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var response = await swapUc.ExecuteAsync(
            new SwapLettersRequest(gameId, aliceId, ['L']));

        response.GameState.CurrentPlayerIndex.Should().Be(1,
            because: "échanger des lettres consomme le tour");
    }

    [Fact]
    public async Task SwapLetters_UnknownGame_ThrowsGameException()
    {
        var (_, _, _, createUc, _, _, _, _, _) = await GameFixture.CreateReadyGame();
        var swapUc = new SwapLettersUseCase(createUc);

        var act = async () => await swapUc.ExecuteAsync(
            new SwapLettersRequest("id-inconnu", "player-id", ['A']));

        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task SwapLetters_NotCurrentPlayer_ThrowsGameException()
    {
        var (gameId, _, bobId, _, _, _, _, swapUc, _) =
            await GameFixture.CreateReadyGame();

        var act = async () => await swapUc.ExecuteAsync(
            new SwapLettersRequest(gameId, bobId, ['A']));

        await act.Should().ThrowAsync<GameException>(
            because: "un joueur ne peut pas echanger hors de son tour");
    }

    [Fact]
    public async Task SwapLetters_LetterNotInRack_ThrowsGameException()
    {
        var (gameId, aliceId, _, createUc, _, _, _, swapUc, _) =
            await GameFixture.CreateReadyGame();

        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var act = async () => await swapUc.ExecuteAsync(
            new SwapLettersRequest(gameId, aliceId, ['Z']));

        await act.Should().ThrowAsync<GameException>(
            because: "les lettres a echanger doivent etre presentes dans le rack");
    }

    [Fact]
    public async Task SwapLetters_WithSwapAll_UsesWholeRack()
    {
        var (gameId, aliceId, _, createUc, _, _, _, swapUc, _) =
            await GameFixture.CreateReadyGame();

        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var response = await swapUc.ExecuteAsync(
            new SwapLettersRequest(gameId, aliceId, SwapAll: true));

        response.NewRack.Should().HaveCount(7);
        response.GameState.CurrentPlayerIndex.Should().Be(1,
            because: "un echange complet consomme le tour");
    }

    #endregion

    #region EndGameUseCase

    [Fact]
    public async Task EndGame_SetsIsGameOverToTrue()
    {
        var (gameId, _, _, _, _, _, _, _, endUc) =
            await GameFixture.CreateReadyGame();

        var response = await endUc.ExecuteAsync(new EndGameRequest(gameId));

        response.FinalState.IsGameOver.Should().BeTrue();
    }

    [Fact]
    public async Task EndGame_ReturnsScoresSortedDescending()
    {
        var (gameId, _, _, _, _, _, _, _, endUc) =
            await GameFixture.CreateReadyGame();

        var response = await endUc.ExecuteAsync(new EndGameRequest(gameId));

        response.Scores.Should().BeInDescendingOrder(s => s.Score,
            because: "les scores finaux doivent être triés du meilleur au moins bon");
    }

    [Fact]
    public async Task EndGame_WinnerIsPlayerWithHighestScore()
    {
        var (gameId, aliceId, _, createUc, _, playUc, _, _, endUc) =
            await GameFixture.CreateReadyGame();

        // Alice joue LA pour marquer des points
        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);
        await playUc.ExecuteAsync(new PlayMoveRequest(gameId, aliceId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            }));

        var response = await endUc.ExecuteAsync(new EndGameRequest(gameId));

        response.Winner.Should().Be("Alice",
            because: "Alice a des points, Bob en a 0");
    }

    [Fact]
    public async Task EndGame_WinnerIsNull_WhenTie()
    {
        var (gameId, _, _, _, _, _, _, _, endUc) =
            await GameFixture.CreateReadyGame();

        // Aucun coup joué → tout le monde a 0 → égalité
        var response = await endUc.ExecuteAsync(new EndGameRequest(gameId));

        response.Winner.Should().BeNull(
            because: "quand tous les scores sont égaux, il n'y a pas de gagnant unique");
    }

    [Fact]
    public async Task EndGame_UnknownGame_ThrowsGameException()
    {
        var (_, _, _, createUc, _, _, _, _, _) = await GameFixture.CreateReadyGame();
        var endUc = new EndGameUseCase(createUc);

        var act = async () => await endUc.ExecuteAsync(new EndGameRequest("id-inconnu"));

        await act.Should().ThrowAsync<GameException>();
    }

    #endregion
}
