using Lama.Console.Commands.Player;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Infrastructure.Profile;
using Lama.Infrastructure.Rating;
using Lama.Infrastructure.Session;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Console.UnitTests;

public sealed class PlayerProfileCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ISessionService _sessionService;
    private readonly IPlayerProfileService _profileService;
    private readonly IPlayerRatingService _ratingService;

    public PlayerProfileCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LamaPlayerProfileCommandTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir);

        _sessionService = new SessionService(NullLogger<SessionService>.Instance);
        _profileService = new JsonPlayerProfileService(NullLogger<JsonPlayerProfileService>.Instance);

        var ratingRepo = new PlayerRatingRepository(NullLogger<PlayerRatingRepository>.Instance);
        var resultRepo = new GameResultRepository(NullLogger<GameResultRepository>.Instance);
        _ratingService = new PlayerRatingService(ratingRepo, resultRepo, NullLogger<PlayerRatingService>.Instance);
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
            // best effort
        }
    }

    [Fact]
    public async Task PlayerCreate_WithOptionalFields_PersistsProfile()
    {
        var cmd = new PlayerCreateCommand(_sessionService, _profileService, NullLogger<PlayerCreateCommand>.Instance);
        var ctx = new CommandContext
        {
            Group = "player",
            Action = "create",
            CommandId = "player.create",
            Arguments = ["Carla"],
            Options = new Dictionary<string, string?>
            {
                ["pseudo"] = "LamaGirl",
                ["country"] = "FR",
                ["region"] = "Bretagne",
                ["birth-year"] = "1994"
            }
        };

        var (_, _, code) = await CaptureAsync(() => cmd.ExecuteAsync(ctx));
        code.Should().Be(ExitCodes.Success);

        var session = _sessionService.LoadSession();
        session.Should().NotBeNull();
        var profile = await _profileService.GetByIdAsync(session!.PlayerId!);
        profile.Should().NotBeNull();
        profile!.Pseudo.Should().Be("LamaGirl");
        profile.Country.Should().Be("FR");
        profile.Region.Should().Be("Bretagne");
        profile.BirthYear.Should().Be(1994);
    }

    [Fact]
    public async Task PlayerUpdate_UpdatesCurrentSessionProfile()
    {
        var create = new PlayerCreateCommand(_sessionService, _profileService, NullLogger<PlayerCreateCommand>.Instance);
        await create.ExecuteAsync(new CommandContext
        {
            CommandId = "player.create",
            Arguments = ["Alice"]
        });

        var update = new PlayerUpdateCommand(_profileService, _sessionService, NullLogger<PlayerUpdateCommand>.Instance);
        var updateContext = new CommandContext
        {
            CommandId = "player.update",
            PlayerId = _sessionService.LoadSession()!.PlayerId,
            Options = new Dictionary<string, string?>
            {
                ["pseudo"] = "Queen",
                ["country"] = "CA"
            }
        };

        var (_, _, code) = await CaptureAsync(() => update.ExecuteAsync(updateContext));
        code.Should().Be(ExitCodes.Success);

        var profile = await _profileService.GetByIdAsync(updateContext.PlayerId!);
        profile.Should().NotBeNull();
        profile!.Pseudo.Should().Be("Queen");
        profile.Country.Should().Be("CA");
    }

    [Fact]
    public async Task PlayerShow_PrintsCombinedProfileAndRating()
    {
        var playerId = "alice-profile";
        await _profileService.SaveAsync(new PlayerProfile(
            PlayerId: playerId,
            DisplayName: "Alice",
            Pseudo: "LamaA",
            Country: "FR",
            Region: "Occitanie",
            BirthYear: 2000,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        await _ratingService.UpdateRatingsAsync([
            new GameResult("g1", playerId, "Alice", 1, false, 300, ["bob"], [1200], DateTimeOffset.UtcNow, 600),
            new GameResult("g1", "bob", "Bob", 2, false, 200, [playerId], [1200], DateTimeOffset.UtcNow, 600)
        ]);

        var show = new PlayerShowCommand(_profileService, _ratingService, NullLogger<PlayerShowCommand>.Instance);
        var ctx = new CommandContext
        {
            CommandId = "player.show",
            Arguments = [playerId]
        };

        var (stdout, stderr, code) = await CaptureAsync(() => show.ExecuteAsync(ctx));
        code.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("PROFIL JOUEUR");
        stdout.Should().Contain("LamaA");
        stdout.Should().Contain("RATING");
    }

    [Fact]
    public async Task PlayerList_Text_ReturnsProfiles()
    {
        await _profileService.SaveAsync(new PlayerProfile(
            PlayerId: "p-list-1",
            DisplayName: "Alice",
            Pseudo: "A",
            Country: "FR",
            Region: "Occitanie",
            BirthYear: 1998,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        await _profileService.SaveAsync(new PlayerProfile(
            PlayerId: "p-list-2",
            DisplayName: "Bob",
            Pseudo: "B",
            Country: "CA",
            Region: "Quebec",
            BirthYear: 1996,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        var list = new PlayerListCommand(_profileService, _ratingService, NullLogger<PlayerListCommand>.Instance);
        var ctx = new CommandContext { CommandId = "player.list" };

        var (stdout, stderr, code) = await CaptureAsync(() => list.ExecuteAsync(ctx));
        code.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("JOUEURS");
        stdout.Should().Contain("Alice");
        stdout.Should().Contain("Bob");
    }

    [Fact]
    public async Task PlayerList_FilterByCountry_ReturnsOnlyMatchingProfile()
    {
        await _profileService.SaveAsync(new PlayerProfile(
            PlayerId: "p-filter-1",
            DisplayName: "Claire",
            Country: "FR",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        await _profileService.SaveAsync(new PlayerProfile(
            PlayerId: "p-filter-2",
            DisplayName: "Diego",
            Country: "ES",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow));

        var list = new PlayerListCommand(_profileService, _ratingService, NullLogger<PlayerListCommand>.Instance);
        var ctx = new CommandContext
        {
            CommandId = "player.list",
            Options = new Dictionary<string, string?>
            {
                ["country"] = "FR",
                ["output"] = "json"
            }
        };

        var (stdout, stderr, code) = await CaptureAsync(() => list.ExecuteAsync(ctx));
        code.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Claire");
        stdout.Should().NotContain("Diego");
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

