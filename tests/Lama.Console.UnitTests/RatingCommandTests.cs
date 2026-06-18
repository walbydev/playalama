using Lama.Console.Commands.Rating;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Infrastructure.Rating;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Console.UnitTests;

public sealed class RatingCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IPlayerRatingService _ratingService;

    public RatingCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LamaRatingCommandTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir);

        var ratingRepo = new PlayerRatingRepository(NullLogger<PlayerRatingRepository>.Instance);
        var resultRepo = new GameResultRepository(NullLogger<GameResultRepository>.Instance);
        _ratingService = new PlayerRatingService(
            ratingRepo,
            resultRepo,
            NullLogger<PlayerRatingService>.Instance);
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
    public async Task RatingShow_UsesContextPlayerId_WhenArgumentMissing()
    {
        await SeedRatingsAsync();

        var command = new RatingShowCommand(_ratingService, NullLogger<RatingShowCommand>.Instance);
        var context = new CommandContext
        {
            Group = "rating",
            Action = "show",
            CommandId = "rating.show",
            PlayerId = "alice"
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => command.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("alice");
        stdout.Should().Contain("Elo");
    }

    [Fact]
    public async Task RatingLeaderboard_InvalidTop_ReturnsInvalidArgument()
    {
        var command = new RatingLeaderboardCommand(_ratingService, NullLogger<RatingLeaderboardCommand>.Instance);
        var context = new CommandContext
        {
            Group = "rating",
            Action = "leaderboard",
            CommandId = "rating.leaderboard",
            Options = new Dictionary<string, string?> { ["top"] = "-1" }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => command.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.InvalidArgument);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("--top");
    }

    [Fact]
    public async Task RatingStats_JsonScope30Days_IsReturned()
    {
        await SeedRatingsAsync();

        var command = new RatingStatsCommand(_ratingService, NullLogger<RatingStatsCommand>.Instance);
        var context = new CommandContext
        {
            Group = "rating",
            Action = "stats",
            CommandId = "rating.stats",
            Arguments = ["alice"],
            Options = new Dictionary<string, string?>
            {
                ["30d"] = null,
                ["output"] = "json"
            }
        };

        var (stdout, stderr, exitCode) = await CaptureAsync(() => command.ExecuteAsync(context));

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("\"scope\": \"30d\"");
        stdout.Should().Contain("\"PlayerId\": \"alice\"");
    }

    private async Task SeedRatingsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        await _ratingService.UpdateRatingsAsync(
        [
            new GameResult(
                GameId: "g1",
                PlayerId: "alice",
                PlayerName: "Alice",
                Rank: 1,
                IsAbandoned: false,
                Score: 320,
                OpponentIds: ["bob"],
                OpponentRatings: [1200],
                PlayedAt: now.AddDays(-3),
                DurationSeconds: 600),
            new GameResult(
                GameId: "g1",
                PlayerId: "bob",
                PlayerName: "Bob",
                Rank: 2,
                IsAbandoned: false,
                Score: 210,
                OpponentIds: ["alice"],
                OpponentRatings: [1200],
                PlayedAt: now.AddDays(-3),
                DurationSeconds: 600)
        ]);
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

