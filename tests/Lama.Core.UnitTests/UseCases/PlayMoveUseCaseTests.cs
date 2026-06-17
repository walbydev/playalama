using FluentAssertions;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.UseCases;

/// <summary>
/// Tests unitaires pour <see cref="PlayMoveUseCase"/>.
/// </summary>
public class PlayMoveUseCaseTests
{
    #region Coup valide

    [Fact]
    public async Task Execute_ValidFirstMove_UpdatesBoard()
    {
        var (gameId, aliceId, _, createUc, _, playUc, passUc, _, _) =
            await GameFixture.CreateReadyGame();

        // Force le rack d'Alice avec L et A
        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var request = new PlayMoveRequest(gameId, aliceId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            });

        var response = await playUc.ExecuteAsync(request);

        response.GameState.Board.Grid[7, 7]!.Letter.Should().Be('L');
        response.GameState.Board.Grid[7, 8]!.Letter.Should().Be('A');
    }

    [Fact]
    public async Task Execute_ValidFirstMove_ReturnsPositiveScore()
    {
        var (gameId, aliceId, _, createUc, _, playUc, _, _, _) =
            await GameFixture.CreateReadyGame();

        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var request = new PlayMoveRequest(gameId, aliceId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            });

        var response = await playUc.ExecuteAsync(request);

        response.Score.Should().BeGreaterThan(0,
            because: "LA depuis H8 (DW) doit rapporter des points");
    }

    [Fact]
    public async Task Execute_ValidMove_PlayerRackIsRefilled()
    {
        var (gameId, aliceId, _, createUc, _, playUc, _, _, _) =
            await GameFixture.CreateReadyGame();

        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var request = new PlayMoveRequest(gameId, aliceId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            });

        var response = await playUc.ExecuteAsync(request);

        response.NewRack.Should().HaveCount(7,
            because: "le rack se recharge après un coup");
    }

    [Fact]
    public async Task Execute_ValidMove_AdvancesToNextPlayer()
    {
        var (gameId, aliceId, _, createUc, _, playUc, _, _, _) =
            await GameFixture.CreateReadyGame();

        createUc.GetEngine(gameId)!.ForceRackForTest(0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var request = new PlayMoveRequest(gameId, aliceId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            });

        var response = await playUc.ExecuteAsync(request);

        response.GameState.CurrentPlayerIndex.Should().Be(1,
            because: "après le coup d'Alice, c'est au tour de Bob");
    }

    #endregion

    #region Validations

    [Fact]
    public async Task Execute_UnknownGameId_ThrowsGameException()
    {
        var (_, _, _, createUc, _, _, _, _, _) = await GameFixture.CreateReadyGame();
        var playUc = new PlayMoveUseCase(createUc);

        var act = async () => await playUc.ExecuteAsync(new PlayMoveRequest(
            "id-inexistant", "player-id",
            new Dictionary<Position, char> { [new Position(7, 7)] = 'A' }));

        await act.Should().ThrowAsync<GameException>();
    }

    [Fact]
    public async Task Execute_InvalidMove_ThrowsGameException()
    {
        var (gameId, aliceId, _, createUc, _, playUc, _, _, _) =
            await GameFixture.CreateReadyGame();

        // Coup invalide : pas sur H8 pour le premier coup
        var act = async () => await playUc.ExecuteAsync(new PlayMoveRequest(
            gameId, aliceId,
            new Dictionary<Position, char>
            {
                [new Position(0, 0)] = 'L',
                [new Position(0, 1)] = 'A'
            }));

        await act.Should().ThrowAsync<GameException>(
            because: "le premier coup doit passer par H8");
    }

    [Fact]
    public async Task Execute_NotCurrentPlayerTurn_ThrowsGameException()
    {
        var (gameId, aliceId, bobId, createUc, _, playUc, _, _, _) =
            await GameFixture.CreateReadyGame();

        // Bob essaie de jouer avant Alice
        createUc.GetEngine(gameId)!.ForceRackForTest(1, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var act = async () => await playUc.ExecuteAsync(new PlayMoveRequest(
            gameId, bobId,
            new Dictionary<Position, char>
            {
                [new Position(7, 7)] = 'L',
                [new Position(7, 8)] = 'A'
            }));

        await act.Should().ThrowAsync<GameException>(
            because: "Bob ne peut pas jouer si ce n'est pas son tour");
    }

    #endregion
}
