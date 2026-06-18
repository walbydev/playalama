using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Lama.Contracts;

namespace Lama.Console.UnitTests;

public sealed class RealCliE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _repoRoot;
    private readonly string _consoleProjectPath;
    private readonly string _sessionDir;

    public RealCliE2ETests()
    {
        _repoRoot = FindRepoRoot();
        _consoleProjectPath = Path.Combine(_repoRoot, "src", "Console", "Lama.Console", "Lama.Console.csproj");
        _sessionDir = Path.Combine(Path.GetTempPath(), "LamaCliE2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sessionDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_sessionDir))
                Directory.Delete(_sessionDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    [Fact]
    public async Task Cli_RealProcess_FullGameJourney_Works()
    {
        var create = await RunCliAsync("game", "create", "Alice");
        create.ExitCode.Should().Be(0);
        create.StdOut.Should().Contain("Partie créée");

        var join = await RunCliAsync("game", "join", "Bob");
        join.ExitCode.Should().Be(0);
        join.StdOut.Should().Contain("a rejoint la partie");

        var swap = await RunCliAsync("play", "swap", "--all");
        swap.ExitCode.Should().Be(0);
        swap.StdOut.Should().Contain("Echange effectue");

        var show = await RunCliAsync("game", "show", "--output", "json");
        show.ExitCode.Should().Be(0);
        show.StdOut.Should().Contain("\"GameId\"");

        var scores = await RunCliAsync("show", "scores");
        scores.ExitCode.Should().Be(0);
        scores.StdOut.Should().Contain("Scores");

        var end = await RunCliAsync("game", "end");
        end.ExitCode.Should().Be(0);
        end.StdOut.Should().Contain("PARTIE TERMINÉE");
    }

    [Fact]
    public async Task Cli_RealProcess_RemainingStubCommands_Work()
    {
        await File.WriteAllTextAsync(Path.Combine(_sessionDir, "session.json"),
            """
            {
              "gameId": null,
              "playerId": null,
              "playerName": "AdminLocal",
              "role": "admin",
              "gameLevel": null,
              "authToken": null,
              "tokenExpiresAt": null,
              "createdAt": "2026-06-18T00:00:00Z",
              "updatedAt": "2026-06-18T00:00:00Z"
            }
            """);

        var status = await RunCliAsync("system", "status", "--output", "json");
        status.ExitCode.Should().Be(0);
        status.StdOut.Should().Contain("isInitialized");

        var restart = await RunCliAsync("system", "restart");
        restart.ExitCode.Should().Be(0);
        restart.StdOut.Should().Contain("Redémarrage logique terminé");

        var playerCreate = await RunCliAsync("player", "create", "Carla");
        playerCreate.ExitCode.Should().Be(0);
        playerCreate.StdOut.Should().Contain("Profil joueur créé");

        var tournamentCreate = await RunCliAsync("tournament", "create", "OpenLama");
        tournamentCreate.ExitCode.Should().Be(0);
        tournamentCreate.StdOut.Should().Contain("Tournoi créé");
    }

    [Fact]
    public async Task Cli_RealProcess_GameAndShow_JsonCsvFormats_Work()
    {
        var create = await RunCliAsync("game", "create", "Alice");
        create.ExitCode.Should().Be(0);

        var listJson = await RunCliAsync("game", "list", "--output", "json");
        listJson.ExitCode.Should().Be(0);
        listJson.StdOut.Should().Contain("GameId");

        var listCsv = await RunCliAsync("game", "list", "--output", "csv");
        listCsv.ExitCode.Should().Be(0);
        listCsv.StdOut.Should().Contain("gameId,level,players,isGameOver,turnNumber,updatedAt");

        var showCsv = await RunCliAsync("game", "show", "--output", "csv");
        showCsv.ExitCode.Should().Be(0);
        showCsv.StdOut.Should().Contain("gameId,language,level,isGameOver,turnNumber,currentPlayer,players,tilesOnBoard,updatedAt");

        var join = await RunCliAsync("game", "join", "Bob");
        join.ExitCode.Should().Be(0);

        var scoresJson = await RunCliAsync("show", "scores", "--output", "json");
        scoresJson.ExitCode.Should().Be(0);
        scoresJson.StdOut.Should().Contain("\"name\"");
        scoresJson.StdOut.Should().Contain("\"score\"");
    }

    [Fact]
    public async Task Cli_RealProcess_PlayCheckThenMove_CrossingLetter_RemainsConsistent()
    {
        var gameId = Guid.NewGuid().ToString("N");
        var playerId = Guid.NewGuid().ToString("N");

        SeedCrossingScenario(gameId, playerId);

        var check = await RunCliAsync("play", "check", "I8", "AS", "V");
        check.ExitCode.Should().Be(0);
        check.StdOut.Should().Contain("Coup valide");

        var move = await RunCliAsync("play", "move", "I8", "AS", "V");
        move.ExitCode.Should().Be(0);
        move.StdOut.Should().Contain("joué en I8 V");
        move.StdErr.Should().NotContain("Coup invalide");
    }

    [Fact]
    public async Task Cli_RealProcess_PlayMove_CrossingExistingWildcard_ReportsExpectedScore()
    {
        var gameId = Guid.NewGuid().ToString("N");
        var playerId = Guid.NewGuid().ToString("N");

        SeedCrossingWildcardScenario(gameId, playerId);

        var move = await RunCliAsync("play", "move", "I8", "LA", "V");
        move.ExitCode.Should().Be(0);
        move.StdOut.Should().Contain("joué en I8 V");
        move.StdOut.Should().Contain("2 pts");

        var scores = await RunCliAsync("show", "scores", "--output", "json");
        scores.ExitCode.Should().Be(0);
        scores.StdOut.Should().Contain("\"name\":\"Alice\"");
        scores.StdOut.Should().Contain("\"score\":2");
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _repoRoot
        };

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(_consoleProjectPath);
        psi.ArgumentList.Add("--");
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        psi.Environment["LAMA_SESSION_DIR"] = _sessionDir;

        using var process = Process.Start(psi)!;
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        return (process.ExitCode, stdOut, stdErr);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "Lama.slnx");
            if (File.Exists(marker))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Impossible de localiser la racine du repository.");
    }

    private void SeedCrossingScenario(string gameId, string playerId)
    {
        var session = new SessionContext(
            GameId: gameId,
            PlayerId: playerId,
            PlayerName: "Alice",
            Role: Role.Host,
            GameLevel: GameLevel.Casual,
            AuthToken: null,
            TokenExpiresAt: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        var persisted = new PersistedGame(
            GameId: gameId,
            Language: "fr",
            GameLevel: GameLevel.Casual,
            IsFirstMove: false,
            IsGameOver: false,
            CurrentPlayerIndex: 0,
            TurnNumber: 2,
            Players:
            [
                // Rack sans 'A': la lettre de croisement ne doit pas etre consommee.
                new PersistedPlayer(playerId, "Alice", 0, ['S', 'B', 'C', 'D', 'E', 'F', 'G'])
            ],
            Board:
            [
                // Mot existant "LA" horizontal: H8='L', I8='A'.
                new PersistedTile(7, 7, 'L'),
                new PersistedTile(7, 8, 'A')
            ],
            RemainingTiles: ['A', 'A', 'A', 'A', 'A'],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            History: [],
            LastMoveSnapshot: null);

        File.WriteAllText(Path.Combine(_sessionDir, "session.json"),
            JsonSerializer.Serialize(session, JsonOptions));

        var gamesDir = Path.Combine(_sessionDir, "games");
        Directory.CreateDirectory(gamesDir);
        File.WriteAllText(Path.Combine(gamesDir, $"{gameId}.json"),
            JsonSerializer.Serialize(persisted, JsonOptions));
    }

    private void SeedCrossingWildcardScenario(string gameId, string playerId)
    {
        var session = new SessionContext(
            GameId: gameId,
            PlayerId: playerId,
            PlayerName: "Alice",
            Role: Role.Host,
            GameLevel: GameLevel.Casual,
            AuthToken: null,
            TokenExpiresAt: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        var persisted = new PersistedGame(
            GameId: gameId,
            Language: "fr",
            GameLevel: GameLevel.Casual,
            IsFirstMove: false,
            IsGameOver: false,
            CurrentPlayerIndex: 0,
            TurnNumber: 2,
            Players:
            [
                // Rack sans 'L': la lettre croisee provient de la tuile deja sur plateau.
                new PersistedPlayer(playerId, "Alice", 0, ['A', 'B', 'C', 'D', 'E', 'F', 'G'])
            ],
            Board:
            [
                // Mot existant "AL" horizontal: H8='A', I8='L' issu d'un joker.
                new PersistedTile(7, 7, 'A'),
                new PersistedTile(7, 8, 'L', IsWildcard: true)
            ],
            RemainingTiles: ['A', 'A', 'A', 'A', 'A'],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            History: [],
            LastMoveSnapshot: null);

        File.WriteAllText(Path.Combine(_sessionDir, "session.json"),
            JsonSerializer.Serialize(session, JsonOptions));

        var gamesDir = Path.Combine(_sessionDir, "games");
        Directory.CreateDirectory(gamesDir);
        File.WriteAllText(Path.Combine(gamesDir, $"{gameId}.json"),
            JsonSerializer.Serialize(persisted, JsonOptions));
    }
}

