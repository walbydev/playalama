using FluentAssertions;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.UseCases;

/// <summary>
/// Tests unitaires pour <see cref="JoinGameUseCase"/>.
/// Vérifie qu'un joueur peut rejoindre une partie existante.
/// </summary>
public class JoinGameUseCaseTests
{
    private static readonly IReadOnlySet<string> Dictionary =
        new HashSet<string> { "LA", "LAMA", "MA", "MOT" };

    private static readonly IReadOnlyDictionary<char, int> Scores =
        new Dictionary<char, int> { ['A'] = 1, ['L'] = 1, ['M'] = 2,
            ['O'] = 1, ['T'] = 1, ['I'] = 1, ['S'] = 1, ['N'] = 1,
            ['R'] = 1, ['*'] = 0 };

    private static readonly IReadOnlyDictionary<char, int> Distribution =
        new Dictionary<char, int> { ['A'] = 9, ['L'] = 5, ['M'] = 3,
            ['O'] = 6, ['T'] = 6, ['I'] = 8, ['S'] = 6, ['N'] = 6,
            ['R'] = 6, ['*'] = 2 };

    private static (CreateGameUseCase createUc, JoinGameUseCase joinUc) CreateSuts()
    {
        var createUc = new CreateGameUseCase(Dictionary, Scores, Distribution,
            new InMemoryGameRepository());
        var joinUc   = new JoinGameUseCase(createUc);
        return (createUc, joinUc);
    }

    #region Rejoindre une partie

    [Fact]
    public async Task Execute_ValidRequest_ReturnsPlayerId()
    {
        var (createUc, joinUc) = CreateSuts();
        var game = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));

        var response = await joinUc.ExecuteAsync(
            new JoinGameRequest(game.GameId, "Bob"));

        response.PlayerId.Should().NotBeNullOrEmpty(
            because: "chaque joueur doit avoir un identifiant unique");
    }

    [Fact]
    public async Task Execute_ValidRequest_PlayerReceives7Letters()
    {
        var (createUc, joinUc) = CreateSuts();
        var game = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));

        var response = await joinUc.ExecuteAsync(
            new JoinGameRequest(game.GameId, "Bob"));

        response.Rack.Should().HaveCount(7,
            because: "tout joueur rejoint avec 7 lettres");
    }

    [Fact]
    public async Task Execute_ValidRequest_GameStateHasTwoPlayers()
    {
        var (createUc, joinUc) = CreateSuts();
        var game = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));

        var response = await joinUc.ExecuteAsync(
            new JoinGameRequest(game.GameId, "Bob"));

        response.GameState.Players.Should().HaveCount(2,
            because: "après que Bob rejoint, la partie a 2 joueurs");
        response.GameState.Players.Should().Contain(p => p.Name == "Bob");
    }

    [Fact]
    public async Task Execute_TwoPlayersJoin_BothPresent()
    {
        var (createUc, joinUc) = CreateSuts();
        var game = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));

        await joinUc.ExecuteAsync(new JoinGameRequest(game.GameId, "Bob"));
        var response = await joinUc.ExecuteAsync(
            new JoinGameRequest(game.GameId, "Charlie"));

        response.GameState.Players.Should().HaveCount(3);
        response.GameState.Players.Should().Contain(p => p.Name == "Bob");
        response.GameState.Players.Should().Contain(p => p.Name == "Charlie");
    }

    [Fact]
    public async Task Execute_TwoPlayersJoin_HaveDifferentPlayerIds()
    {
        var (createUc, joinUc) = CreateSuts();
        var game = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));

        var r1 = await joinUc.ExecuteAsync(new JoinGameRequest(game.GameId, "Bob"));
        var r2 = await joinUc.ExecuteAsync(new JoinGameRequest(game.GameId, "Charlie"));

        r1.PlayerId.Should().NotBe(r2.PlayerId,
            because: "chaque joueur doit avoir un ID unique");
    }

    #endregion

    #region Validations

    [Fact]
    public async Task Execute_UnknownGameId_ThrowsGameException()
    {
        var (_, joinUc) = CreateSuts();

        var act = async () => await joinUc.ExecuteAsync(
            new JoinGameRequest("id-inexistant", "Bob"));

        await act.Should().ThrowAsync<GameException>(
            because: "rejoindre une partie inexistante doit lever une exception");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_EmptyPlayerName_ThrowsGameException(string name)
    {
        var (createUc, joinUc) = CreateSuts();
        var game = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));

        var act = async () => await joinUc.ExecuteAsync(
            new JoinGameRequest(game.GameId, name));

        await act.Should().ThrowAsync<GameException>(
            because: "le nom du joueur ne peut pas être vide");
    }

    #endregion
}
