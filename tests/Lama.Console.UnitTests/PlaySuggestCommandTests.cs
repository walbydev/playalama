using Lama.Console.Commands.Play;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Lama.Infrastructure.Persistence;
using Lama.Infrastructure.Session;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Console.UnitTests;

public sealed class PlaySuggestCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ISessionService _sessionService;
    private readonly CreateGameUseCase _createGameUseCase;
    private readonly PlaySuggestCommand _command;

    public PlaySuggestCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LamaPlaySuggestTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir);

        _sessionService = new SessionService(NullLogger<SessionService>.Instance);
        var repository = new JsonGameRepository(NullLogger<JsonGameRepository>.Instance);

        var dictionary = new HashSet<string> { "LA", "AL" };
        var scores = new Dictionary<char, int> { ['L'] = 1, ['A'] = 1, ['*'] = 0 };
        var distribution = new Dictionary<char, int> { ['L'] = 5, ['A'] = 5, ['*'] = 2 };

        _createGameUseCase = new CreateGameUseCase(dictionary, scores, distribution, repository);
        var suggestUseCase = new SuggestMovesUseCase(_createGameUseCase);
        _command = new PlaySuggestCommand(suggestUseCase, NullLogger<PlaySuggestCommand>.Instance);
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
            // best effort cleanup
        }
    }

    [Fact]
    public async Task PlaySuggest_LocalStub_ReturnsSuccess_WhenSessionIsActive()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));
        _sessionService.SaveSession(new SessionContext(
            GameId: created.GameId,
            PlayerId: created.HostPlayerId,
            PlayerName: "Alice",
            Role: Role.Host,
            GameLevel: GameLevel.Casual,
            AuthToken: null,
            TokenExpiresAt: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        var context = new CommandContext
        {
            Group = "play",
            Action = "suggest",
            CommandId = "play.suggest",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Casual,
            Options = new Dictionary<string, string?>
            {
                ["top"] = "2"
            }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _command.ExecuteAsync(context));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.NotEqual(string.Empty, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public async Task PlaySuggest_InvalidTop_ReturnsInvalidArgument()
    {
        var created = await _createGameUseCase.ExecuteAsync(new CreateGameRequest("Alice"));

        var context = new CommandContext
        {
            Group = "play",
            Action = "suggest",
            CommandId = "play.suggest",
            GameId = created.GameId,
            PlayerId = created.HostPlayerId,
            PlayerName = "Alice",
            Role = Role.Host,
            GameLevel = GameLevel.Casual,
            Options = new Dictionary<string, string?>
            {
                ["top"] = "0"
            }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => _command.ExecuteAsync(context));

        Assert.Equal(ExitCodes.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("--top", stderr);
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

