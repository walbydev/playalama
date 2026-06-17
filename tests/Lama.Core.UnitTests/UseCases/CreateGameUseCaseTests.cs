using FluentAssertions;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UnitTests.Helpers;
using Lama.Core.UseCases;
using Lama.Domain.Engine;

namespace Lama.Core.UnitTests.UseCases;

/// <summary>
/// Tests unitaires pour <see cref="CreateGameUseCase"/>.
/// Vérifie la création d'une partie et l'initialisation de l'état.
/// </summary>
public class CreateGameUseCaseTests
{
    private static readonly IReadOnlySet<string> Dictionary =
        new HashSet<string> { "LA", "LAMA", "MA", "MOT" };

    private static readonly IReadOnlyDictionary<char, int> Scores =
        new Dictionary<char, int>
        {
            ['A'] = 1, ['L'] = 1, ['M'] = 2, ['O'] = 1, ['T'] = 1,
            ['I'] = 1, ['S'] = 1, ['N'] = 1, ['R'] = 1, ['*'] = 0
        };

    private static readonly IReadOnlyDictionary<char, int> Distribution =
        new Dictionary<char, int>
        {
            ['A'] = 9, ['L'] = 5, ['M'] = 3, ['O'] = 6,
            ['T'] = 6, ['I'] = 8, ['S'] = 6, ['N'] = 6,
            ['R'] = 6, ['*'] = 2
        };

    private static CreateGameUseCase CreateSut() =>
        new(Dictionary, Scores, Distribution, new InMemoryGameRepository());

    #region Création de base

    [Fact]
    public async Task Execute_WithValidRequest_ReturnsGameId()
    {
        var sut     = CreateSut();
        var request = new CreateGameRequest("Alice");

        var response = await sut.ExecuteAsync(request);

        response.GameId.Should().NotBeNullOrEmpty(
            because: "une partie créée doit avoir un identifiant unique");
    }

    [Fact]
    public async Task Execute_WithValidRequest_ReturnsHostPlayerId()
    {
        var sut     = CreateSut();
        var request = new CreateGameRequest("Alice");

        var response = await sut.ExecuteAsync(request);

        response.HostPlayerId.Should().NotBeNullOrEmpty(
            because: "l'hôte doit avoir un identifiant unique");
    }

    [Fact]
    public async Task Execute_TwoCalls_ProduceDifferentGameIds()
    {
        var sut = CreateSut();

        var r1 = await sut.ExecuteAsync(new CreateGameRequest("Alice"));
        var r2 = await sut.ExecuteAsync(new CreateGameRequest("Bob"));

        r1.GameId.Should().NotBe(r2.GameId,
            because: "chaque partie doit avoir un identifiant unique");
    }

    [Fact]
    public async Task Execute_InitialState_HasEmptyBoard()
    {
        var sut      = CreateSut();
        var response = await sut.ExecuteAsync(new CreateGameRequest("Alice"));

        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
                response.InitialState.Board.Grid[r, c].Should().BeNull(
                    because: "le plateau est vide en début de partie");
    }

    [Fact]
    public async Task Execute_InitialState_HasOnePlayer_TheHost()
    {
        var sut      = CreateSut();
        var response = await sut.ExecuteAsync(new CreateGameRequest("Alice"));

        response.InitialState.Players.Should().HaveCount(1,
            because: "seul l'hôte est présent avant que les autres rejoignent");
        response.InitialState.Players[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Execute_InitialState_HostHas7Letters()
    {
        var sut      = CreateSut();
        var response = await sut.ExecuteAsync(new CreateGameRequest("Alice"));

        response.InitialState.Players[0].Rack.Should().HaveCount(7,
            because: "l'hôte reçoit 7 lettres dès la création");
    }

    [Fact]
    public async Task Execute_InitialState_TurnNumberIsOne()
    {
        var sut      = CreateSut();
        var response = await sut.ExecuteAsync(new CreateGameRequest("Alice"));

        response.InitialState.TurnNumber.Should().Be(1);
    }

    [Fact]
    public async Task Execute_InitialState_IsNotGameOver()
    {
        var sut      = CreateSut();
        var response = await sut.ExecuteAsync(new CreateGameRequest("Alice"));

        response.InitialState.IsGameOver.Should().BeFalse();
    }

    #endregion

    #region Validation de la requête

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_WithEmptyHostName_ThrowsGameException(string name)
    {
        var sut = CreateSut();

        var act = async () => await sut.ExecuteAsync(new CreateGameRequest(name));

        await act.Should().ThrowAsync<GameException>(
            because: "le nom de l'hôte ne peut pas être vide");
    }

    #endregion

    #region Accès au GameEngine créé

    [Fact]
    public async Task Execute_GameEngineIsRetrievable_ById()
    {
        var sut      = CreateSut();
        var response = await sut.ExecuteAsync(new CreateGameRequest("Alice"));

        var engine = sut.GetEngine(response.GameId);

        engine.Should().NotBeNull(
            because: "le moteur de jeu doit être récupérable après création");
    }

    [Fact]
    public async Task GetEngine_WithUnknownId_ReturnsNull()
    {
        var sut = CreateSut();

        var engine = sut.GetEngine("id-inexistant");

        engine.Should().BeNull();
    }

    #endregion
}
