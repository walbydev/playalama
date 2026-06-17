using System.Text.Json;
using Lama.Console.Commands.Game;
using Lama.Console.Commands.Play;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Lama.Infrastructure.Persistence;
using Lama.Infrastructure.Session;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Console.UnitTests;

public sealed class GameAndPlayCommandTests : IDisposable
{
    private static readonly IReadOnlySet<string> Dictionary =
        new HashSet<string>
        {
            "LA", "MA", "AB", "BA", "AA", "AL", "AM", "SA"
        };

    private static readonly IReadOnlyDictionary<char, int> Scores =
        new Dictionary<char, int>
        {
            ['A'] = 1, ['B'] = 3, ['C'] = 3, ['D'] = 2, ['E'] = 1,
            ['F'] = 4, ['G'] = 2, ['L'] = 1, ['M'] = 2, ['S'] = 1,
            ['U'] = 1, ['W'] = 4, ['*'] = 0
        };

    private static readonly IReadOnlyDictionary<char, int> MultiPlayerDistribution =
        new Dictionary<char, int>
        {
            ['A'] = 3, ['B'] = 3, ['C'] = 3, ['D'] = 3,
            ['E'] = 3, ['F'] = 3, ['G'] = 3
        };

    private readonly string _tempDir;
    private readonly ISessionService _sessionService;
    private readonly IGameRepository _gameRepository;
    private readonly CreateGameUseCase _createGameUseCase;
    private readonly JoinGameUseCase _joinGameUseCase;
    private readonly GameCreateCommand _gameCreateCommand;
    private readonly GameJoinCommand _gameJoinCommand;
    private readonly GameListCommand _gameListCommand;
    private readonly GameShowCommand _gameShowCommand;
    private readonly GameSaveCommand _gameSaveCommand;
    private readonly GamePauseCommand _gamePauseCommand;
    private readonly GameEndCommand _gameEndCommand;
    private readonly PlayPassCommand _playPassCommand;
    private readonly PlaySwapCommand _playSwapCommand;

    public GameAndPlayCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LamaConsoleCommandTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir);

        _sessionService = new SessionService(NullLogger<SessionService>.Instance);
        _gameRepository = new JsonGameRepository(NullLogger<JsonGameRepository>.Instance);
        _createGameUseCase = new CreateGameUseCase(Dictionary, Scores, MultiPlayerDistribution, _gameRepository);
        _joinGameUseCase = new JoinGameUseCase(_createGameUseCase);

        _gameCreateCommand = new GameCreateCommand(_createGameUseCase, _sessionService, NullLogger<GameCreateCommand>.Instance);
        _gameJoinCommand = new GameJoinCommand(_joinGameUseCase, _sessionService, NullLogger<GameJoinCommand>.Instance);
        _gameListCommand = new GameListCommand(_gameRepository, NullLogger<GameListCommand>.Instance);
        _gameShowCommand = new GameShowCommand(_gameRepository, NullLogger<GameShowCommand>.Instance);
        _gameSaveCommand = new GameSaveCommand(_createGameUseCase, _sessionService, _gameRepository, NullLogger<GameSaveCommand>.Instance);
        _gamePauseCommand = new GamePauseCommand(_createGameUseCase, _sessionService, NullLogger<GamePauseCommand>.Instance);
        var endGameUseCase = new EndGameUseCase(_createGameUseCase);
        var passTurnUseCase = new PassTurnUseCase(_createGameUseCase);
        var swapLettersUseCase = new SwapLettersUseCase(_createGameUseCase);
        _gameEndCommand = new GameEndCommand(endGameUseCase, _sessionService, NullLogger<GameEndCommand>.Instance);
        _playPassCommand = new PlayPassCommand(passTurnUseCase, NullLogger<PlayPassCommand>.Instance);
        _playSwapCommand = new PlaySwapCommand(swapLettersUseCase, _sessionService, NullLogger<PlaySwapCommand>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // nettoyage best effort
        }
    }

    [Fact]
    public async Task GameCreateCommand_CreatesGame_AndPersistsSession()
    {
        var context = new CommandContext
        {
            Group = "game",
            Action = "create",
            Arguments = ["Alice"],
            Options = new Dictionary<string, string?> { ["level"] = "standard" }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameCreateCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Partie créée");

        var session = _sessionService.LoadSession();
        session.Should().NotBeNull();
        session!.GameId.Should().NotBeNullOrWhiteSpace();
        _gameRepository.Exists(session.GameId!).Should().BeTrue();
    }

    [Fact]
    public async Task GameJoinCommand_AddsSecondPlayer()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");

        var context = new CommandContext
        {
            Group = "game",
            Action = "join",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard,
            Arguments = ["Bob"]
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameJoinCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("a rejoint la partie");
        stdout.Should().Contain("Joueurs : 2");

        _sessionService.LoadSession().Should().NotBeNull();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [InlineData("csv")]
    public async Task GameListCommand_FormatsOutputPerRequestedFormat(string format)
    {
        var first = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        var second = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Bob"));
        _ = (first, second);

        var context = new CommandContext
        {
            Group = "game",
            Action = "list",
            Options = new Dictionary<string, string?> { ["output"] = format }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameListCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();

        if (format == "text")
        {
            stdout.Should().Contain("Parties disponibles");
            stdout.Should().Contain(first.GameId);
            stdout.Should().Contain(second.GameId);
        }
        else if (format == "json")
        {
            using var json = JsonDocument.Parse(stdout);
            json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
            json.RootElement.GetArrayLength().Should().BeGreaterOrEqualTo(2);
        }
        else
        {
            stdout.Should().Contain("gameId,level,players,isGameOver,turnNumber,updatedAt");
            stdout.Should().Contain(first.GameId);
            stdout.Should().Contain(second.GameId);
        }
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [InlineData("csv")]
    public async Task GameShowCommand_FormatsOutputPerRequestedFormat(string format)
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");

        var context = new CommandContext
        {
            Group = "game",
            Action = "show",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard,
            Options = new Dictionary<string, string?> { ["output"] = format }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameShowCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();

        if (format == "text")
        {
            stdout.Should().Contain($"Partie : {created.GameId}");
            stdout.Should().Contain("Joueurs");
        }
        else if (format == "json")
        {
            using var json = JsonDocument.Parse(stdout);
            json.RootElement.EnumerateObject()
                .Select(p => p.Name)
                .Should().Contain(name => name.Equals("gameId", StringComparison.OrdinalIgnoreCase));

            json.RootElement.EnumerateObject()
                .Select(p => p.Name)
                .Should().Contain(name => name.Equals("players", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            stdout.Should().Contain("gameId,language,level,isGameOver,turnNumber,currentPlayer,players,tilesOnBoard,updatedAt");
            stdout.Should().Contain(created.GameId);
        }
    }

    [Fact]
    public async Task GameShowCommand_ReturnsNotFound_WhenGameMissing()
    {
        var context = new CommandContext
        {
            Group = "game",
            Action = "show",
            GameId = "missing-game"
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameShowCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.GameNotFound);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("Partie introuvable");
    }

    [Fact]
    public async Task GameSaveCommand_SavesAndExportsSnapshot()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");

        var exportPath = Path.Combine(_tempDir, "exports", "game-save.json");
        var context = new CommandContext
        {
            Group = "game",
            Action = "save",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard,
            Options = new Dictionary<string, string?> { ["file"] = exportPath }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameSaveCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Partie sauvegardee");
        File.Exists(exportPath).Should().BeTrue();
        _gameRepository.Exists(created.GameId).Should().BeTrue();
        _sessionService.LoadSession().Should().NotBeNull();
    }

    [Fact]
    public async Task GameSaveCommand_ReturnsGameNotFound_WhenNoActiveSession()
    {
        var context = new CommandContext
        {
            Group = "game",
            Action = "save"
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameSaveCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.GameNotFound);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("Aucune partie active");
    }

    [Fact]
    public async Task GamePauseCommand_SavesSnapshot_AndKeepsSession()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");

        var context = new CommandContext
        {
            Group = "game",
            Action = "pause",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gamePauseCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("mise en pause");
        _gameRepository.Exists(created.GameId).Should().BeTrue();
        _sessionService.LoadSession().Should().NotBeNull();
    }

    [Fact]
    public async Task GameEndCommand_EndsGame_AndClearsSession()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");

        var context = new CommandContext
        {
            Group = "game",
            Action = "end",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _gameEndCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("PARTIE TERMINÉE");
        _gameRepository.Exists(created.GameId).Should().BeFalse();
        _sessionService.LoadSession().Should().BeNull();
    }

    [Fact]
    public async Task PlayPassCommand_PassesTurnToNextPlayer()
    {
        var (gameId, hostId, bobId) = await CreateTwoPlayerGameAsync();

        var context = new CommandContext
        {
            Group = "play",
            Action = "pass",
            GameId = gameId,
            PlayerId = hostId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _playPassCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Bob");
        _ = bobId;
    }

    [Fact]
    public async Task PlaySwapCommand_SwapsAllLetters_AndAdvancesTurn()
    {
        var (gameId, hostId, bobId) = await CreateTwoPlayerGameAsync();

        var context = new CommandContext
        {
            Group = "play",
            Action = "swap",
            GameId = gameId,
            PlayerId = hostId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard,
            Options = new Dictionary<string, string?> { ["all"] = null }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _playSwapCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Tour suivant : Bob");
        _ = bobId;
    }

    [Fact]
    public async Task PlayMoveCommand_JoueLePremierCoup_QuandRackEstDeterministe()
    {
        var moveRepository = new JsonGameRepository(NullLogger<JsonGameRepository>.Instance);
        var moveUseCase = new CreateGameUseCase(
            new HashSet<string> { "LA" },
            new Dictionary<char, int> { ['L'] = 1, ['A'] = 1, ['*'] = 0 },
            new Dictionary<char, int> { ['L'] = 1, ['A'] = 6 },
            moveRepository);
        var playMoveUseCase = new PlayMoveUseCase(moveUseCase);
        var playMoveCommand = new PlayMoveCommand(playMoveUseCase, _sessionService, NullLogger<PlayMoveCommand>.Instance);

        var created = await moveUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");

        var context = new CommandContext
        {
            Group = "play",
            Action = "move",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Standard,
            Arguments = ["H8", "LA", "H"]
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => playMoveCommand.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("joué en H8 H");
    }

    private async Task<(string GameId, string HostPlayerId, string BobPlayerId)> CreateTwoPlayerGameAsync()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        var joined = await _joinGameUseCase.ExecuteAsync(new JoinGameRequest(created.GameId, "Bob"));
        SaveHostSession(created.GameId, created.HostPlayerId, "Alice");
        return (created.GameId, created.HostPlayerId, joined.PlayerId);
    }

    private void SaveHostSession(string gameId, string playerId, string playerName)
    {
        _sessionService.SaveSession(new SessionContext(
            GameId:         gameId,
            PlayerId:       playerId,
            PlayerName:     playerName,
            Role:           Role.Host,
            GameLevel:      GameLevel.Standard,
            AuthToken:      null,
            TokenExpiresAt: null,
            CreatedAt:      DateTimeOffset.UtcNow,
            UpdatedAt:      DateTimeOffset.UtcNow));
    }

    private static async Task<(string StdOut, string StdErr, int ExitCode)> CaptureAsync(Func<Task<int>> action)
    {
        var originalOut = System.Console.Out;
        var originalErr = System.Console.Error;

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        System.Console.SetOut(stdout);
        System.Console.SetError(stderr);

        try
        {
            var exitCode = await action();
            return (stdout.ToString(), stderr.ToString(), exitCode);
        }
        finally
        {
            System.Console.SetOut(originalOut);
            System.Console.SetError(originalErr);
        }
    }
}



