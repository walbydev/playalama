using FluentAssertions;
using Lama.Contracts;
using Lama.Infrastructure.Persistence;
using Lama.Infrastructure.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Infrastructure.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="JsonGameRepository"/>.
/// Vérifie la persistance des parties en JSON et la cohérence
/// de la sérialisation/désérialisation.
/// </summary>
[Collection(SequentialTestCollection.Name)]
public class GameRepositoryTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly JsonGameRepository _sut;

    public GameRepositoryTests()
    {
        _tempDir = new TempDirectory();
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir.Path);
        _sut = new JsonGameRepository(NullLogger<JsonGameRepository>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        _tempDir.Dispose();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static PersistedGame CreateTestGame(string gameId = "game-test-001") => new(
        GameId:             gameId,
        Language:           "fr",
        GameLevel:          GameLevel.Standard,
        IsFirstMove:        true,
        IsGameOver:         false,
        CurrentPlayerIndex: 0,
        TurnNumber:         1,
        Players:
        [
            new PersistedPlayer("player-alice", "Alice", 0,  ['L', 'A', 'M', 'A', 'S', 'T', 'I']),
            new PersistedPlayer("player-bob",   "Bob",   0,  ['Z', 'E', 'N', 'O', 'I', 'R', 'S'])
        ],
        Board:          [],
        RemainingTiles: ['A', 'B', 'C', 'D', 'E'],
        CreatedAt:      DateTimeOffset.UtcNow,
        UpdatedAt:      DateTimeOffset.UtcNow);

    #region Save + Load (aller-retour)

    [Fact]
    public void Save_CreatesFileOnDisk()
    {
        var game = CreateTestGame();

        _sut.Save(game);

        _sut.Exists(game.GameId).Should().BeTrue(
            because: "Save doit créer le fichier de partie");
    }

    [Fact]
    public void SaveAndLoad_PreservesGameId()
    {
        var game = CreateTestGame("game-xyz-123");
        _sut.Save(game);

        var loaded = _sut.Load("game-xyz-123");

        loaded.Should().NotBeNull();
        loaded!.GameId.Should().Be("game-xyz-123");
    }

    [Fact]
    public void SaveAndLoad_PreservesAllFields()
    {
        var game = CreateTestGame();
        _sut.Save(game);

        var loaded = _sut.Load(game.GameId);

        loaded.Should().NotBeNull();
        loaded!.Language.Should().Be("fr");
        loaded.GameLevel.Should().Be(GameLevel.Standard);
        loaded.IsFirstMove.Should().BeTrue();
        loaded.IsGameOver.Should().BeFalse();
        loaded.CurrentPlayerIndex.Should().Be(0);
        loaded.TurnNumber.Should().Be(1);
    }

    [Fact]
    public void SaveAndLoad_PreservesPlayers()
    {
        var game = CreateTestGame();
        _sut.Save(game);

        var loaded = _sut.Load(game.GameId);

        loaded!.Players.Should().HaveCount(2);
        loaded.Players[0].PlayerId.Should().Be("player-alice");
        loaded.Players[0].Name.Should().Be("Alice");
        loaded.Players[0].Score.Should().Be(0);
        loaded.Players[0].Rack.Should().BeEquivalentTo(['L', 'A', 'M', 'A', 'S', 'T', 'I']);
        loaded.Players[1].Name.Should().Be("Bob");
    }

    [Fact]
    public void SaveAndLoad_PreservesRemainingTiles()
    {
        var game = CreateTestGame();
        _sut.Save(game);

        var loaded = _sut.Load(game.GameId);

        loaded!.RemainingTiles.Should().BeEquivalentTo(['A', 'B', 'C', 'D', 'E'],
            because: "les tuiles restantes dans le sac doivent être préservées");
    }

    [Fact]
    public void SaveAndLoad_PreservesBoardTiles()
    {
        var game = CreateTestGame() with
        {
            Board =
            [
                new PersistedTile(7, 7, 'L'),
                new PersistedTile(7, 8, 'A'),
                new PersistedTile(7, 9, 'M'),
                new PersistedTile(7, 10, 'A')
            ],
            IsFirstMove = false
        };
        _sut.Save(game);

        var loaded = _sut.Load(game.GameId);

        loaded!.Board.Should().HaveCount(4,
            because: "les 4 tuiles du plateau doivent être préservées");
        loaded.Board[0].Row.Should().Be(7);
        loaded.Board[0].Col.Should().Be(7);
        loaded.Board[0].Letter.Should().Be('L');
        loaded.IsFirstMove.Should().BeFalse();
    }

    [Fact]
    public void SaveAndLoad_PreservesScores()
    {
        var game = CreateTestGame() with
        {
            Players =
            [
                new PersistedPlayer("player-alice", "Alice", 42, ['L', 'A']),
                new PersistedPlayer("player-bob",   "Bob",   17, ['Z', 'E'])
            ]
        };
        _sut.Save(game);

        var loaded = _sut.Load(game.GameId);

        loaded!.Players[0].Score.Should().Be(42);
        loaded.Players[1].Score.Should().Be(17);
    }

    [Fact]
    public void SaveAndLoad_PreservesGameLevel()
    {
        foreach (var level in Enum.GetValues<GameLevel>())
        {
            var game = CreateTestGame() with { GameLevel = level };
            _sut.Save(game);

            var loaded = _sut.Load(game.GameId);
            loaded!.GameLevel.Should().Be(level,
                because: $"le niveau {level} doit être préservé");
        }
    }

    [Fact]
    public void Save_Overwrites_ExistingGame()
    {
        var game = CreateTestGame();
        _sut.Save(game);

        // Mettre à jour le score et sauvegarder à nouveau
        var updated = game with
        {
            Players =
            [
                new PersistedPlayer("player-alice", "Alice", 100, ['A']),
                new PersistedPlayer("player-bob",   "Bob",   50,  ['B'])
            ],
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _sut.Save(updated);

        var loaded = _sut.Load(game.GameId);
        loaded!.Players[0].Score.Should().Be(100,
            because: "la deuxième sauvegarde doit écraser la première");
    }

    [Fact]
    public void SaveAndLoad_PreservesJokerWildcard()
    {
        var game = CreateTestGame() with
        {
            Board = [new PersistedTile(7, 7, 'A', IsWildcard: true)]
        };
        _sut.Save(game);

        var loaded = _sut.Load(game.GameId);

        loaded!.Board[0].IsWildcard.Should().BeTrue(
            because: "le flag joker doit être préservé");
    }

    #endregion

    #region Load — cas limites

    [Fact]
    public void Load_ReturnsNull_WhenGameNotFound()
    {
        var result = _sut.Load("id-inexistant");

        result.Should().BeNull();
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileIsCorrupted()
    {
        var gameId   = "corrupted-game";
        var gamesDir = Path.Combine(_tempDir.Path, "games");
        Directory.CreateDirectory(gamesDir);
        File.WriteAllText(Path.Combine(gamesDir, $"{gameId}.json"), "{ invalid json !!!");

        var result = _sut.Load(gameId);

        result.Should().BeNull(
            because: "un fichier corrompu doit être toléré sans exception");
    }

    #endregion

    #region Delete

    [Fact]
    public void Delete_RemovesFile()
    {
        var game = CreateTestGame();
        _sut.Save(game);

        _sut.Delete(game.GameId);

        _sut.Exists(game.GameId).Should().BeFalse(
            because: "Delete doit supprimer le fichier");
    }

    [Fact]
    public void Delete_IsIdempotent_WhenGameNotFound()
    {
        var act = () => _sut.Delete("id-inexistant");

        act.Should().NotThrow(
            because: "Delete doit être silencieux si la partie n'existe pas");
    }

    [Fact]
    public void Load_ReturnsNull_AfterDelete()
    {
        var game = CreateTestGame();
        _sut.Save(game);
        _sut.Delete(game.GameId);

        var result = _sut.Load(game.GameId);

        result.Should().BeNull();
    }

    #endregion

    #region Exists

    [Fact]
    public void Exists_ReturnsFalse_WhenNoGame()
    {
        _sut.Exists("id-inexistant").Should().BeFalse();
    }

    [Fact]
    public void Exists_ReturnsTrue_AfterSave()
    {
        var game = CreateTestGame();
        _sut.Save(game);

        _sut.Exists(game.GameId).Should().BeTrue();
    }

    #endregion

    #region ListGameIds

    [Fact]
    public void ListGameIds_ReturnsEmpty_WhenNoGames()
    {
        var ids = _sut.ListGameIds();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void ListGameIds_ReturnsAllSavedGameIds()
    {
        _sut.Save(CreateTestGame("game-001"));
        _sut.Save(CreateTestGame("game-002"));
        _sut.Save(CreateTestGame("game-003"));

        var ids = _sut.ListGameIds();

        ids.Should().HaveCount(3);
        ids.Should().Contain("game-001");
        ids.Should().Contain("game-002");
        ids.Should().Contain("game-003");
    }

    [Fact]
    public void ListGameIds_ExcludesDeletedGames()
    {
        _sut.Save(CreateTestGame("game-001"));
        _sut.Save(CreateTestGame("game-002"));
        _sut.Delete("game-001");

        var ids = _sut.ListGameIds();

        ids.Should().HaveCount(1);
        ids.Should().Contain("game-002");
        ids.Should().NotContain("game-001");
    }

    #endregion

    #region Isolation entre instances

    [Fact]
    public void TwoInstances_SharedDirectory_SeeEachOthersGames()
    {
        _sut.Save(CreateTestGame("game-shared"));

        // Deuxième instance dans le même répertoire
        var sut2   = new JsonGameRepository(NullLogger<JsonGameRepository>.Instance);
        var loaded = sut2.Load("game-shared");

        loaded.Should().NotBeNull(
            because: "deux instances partageant le même répertoire voient les mêmes parties");
    }

    #endregion
}
