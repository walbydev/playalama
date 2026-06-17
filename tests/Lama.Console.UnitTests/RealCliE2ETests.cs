using System.Diagnostics;
using FluentAssertions;

namespace Lama.Console.UnitTests;

public sealed class RealCliE2ETests : IDisposable
{
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

        var pass = await RunCliAsync("play", "pass");
        pass.ExitCode.Should().Be(0);
        pass.StdOut.Should().Contain("Tour passé");

        var show = await RunCliAsync("game", "show", "--output", "json");
        show.ExitCode.Should().Be(0);
        show.StdOut.Should().Contain("\"GameId\"");

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
}

