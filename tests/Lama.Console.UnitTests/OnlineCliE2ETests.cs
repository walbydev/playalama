using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Lama.Console.UnitTests;

public sealed class OnlineCliE2ETests : IAsyncLifetime, IDisposable
{
	private readonly string _repoRoot;
	private readonly string _consoleProjectPath;
	private readonly string _serverProjectPath;
	private readonly string _hostSessionDir;
	private readonly string _guestSessionDir;
	private readonly HttpClient _httpClient = new();

	private Process? _serverProcess;
	private string _serverUrl = string.Empty;
	private readonly StringBuilder _serverStdOut = new();
	private readonly StringBuilder _serverStdErr = new();

	public OnlineCliE2ETests()
	{
		_repoRoot = FindRepoRoot();
		_consoleProjectPath = Path.Combine(_repoRoot, "src", "Console", "Lama.Console", "Lama.Console.csproj");
		_serverProjectPath = Path.Combine(_repoRoot, "src", "Server", "Lama.Server", "Lama.Server.csproj");

		_hostSessionDir = Path.Combine(Path.GetTempPath(), "LamaOnlineHost", Guid.NewGuid().ToString("N"));
		_guestSessionDir = Path.Combine(Path.GetTempPath(), "LamaOnlineGuest", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_hostSessionDir);
		Directory.CreateDirectory(_guestSessionDir);
	}

	public async Task InitializeAsync()
	{
		var port = GetFreePort();
		_serverUrl = $"http://127.0.0.1:{port}";

		var psi = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			WorkingDirectory = _repoRoot
		};

		psi.ArgumentList.Add("run");
		psi.ArgumentList.Add("--project");
		psi.ArgumentList.Add(_serverProjectPath);
		psi.ArgumentList.Add("--urls");
		psi.ArgumentList.Add(_serverUrl);

		psi.Environment["LAMA_SERVER_ALLOW_SHUTDOWN"] = "true";
		psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

		_serverProcess = Process.Start(psi)!;
		_serverProcess.OutputDataReceived += (_, e) =>
		{
			if (!string.IsNullOrWhiteSpace(e.Data))
				_serverStdOut.AppendLine(e.Data);
		};
		_serverProcess.ErrorDataReceived += (_, e) =>
		{
			if (!string.IsNullOrWhiteSpace(e.Data))
				_serverStdErr.AppendLine(e.Data);
		};
		_serverProcess.BeginOutputReadLine();
		_serverProcess.BeginErrorReadLine();

		await WaitForServerHealthAsync();
	}

	public async Task DisposeAsync()
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(_serverUrl))
				await _httpClient.PostAsync($"{_serverUrl}/internal/shutdown", content: null);
		}
		catch
		{
			// best effort
		}

		if (_serverProcess is not null && !_serverProcess.HasExited)
		{
			try
			{
				await _serverProcess.WaitForExitAsync();
			}
			catch
			{
				// best effort
			}

			if (!_serverProcess.HasExited)
				_serverProcess.Kill(entireProcessTree: true);
		}
	}

	public void Dispose()
	{
		_httpClient.Dispose();

		try
		{
			if (Directory.Exists(_hostSessionDir))
				Directory.Delete(_hostSessionDir, recursive: true);
			if (Directory.Exists(_guestSessionDir))
				Directory.Delete(_guestSessionDir, recursive: true);
		}
		catch
		{
			// best effort cleanup
		}
	}

	[Fact]
	public async Task Cli_Online_FullTurn_Check_Move_Challenge_Swap_Works()
	{
		var create = await RunCliAsync(_hostSessionDir, "game", "create", "Alice", "--level", "casual");
		create.ExitCode.Should().Be(0);
		create.StdOut.Should().Contain("Partie créée");
		create.StdOut.Should().Contain("Mode      : online");

		var gameId = ExtractGameId(create.StdOut);

		var join = await RunCliAsync(_guestSessionDir, "game", "join", "Bob", "--game-id", gameId);
		join.ExitCode.Should().Be(0);
		join.StdOut.Should().Contain("rejoint la partie online");

		var playableWord = await GetPlayableWordFromRackAsync(gameId);
		playableWord.Length.Should().BeGreaterThanOrEqualTo(2);

		var check = await RunCliAsync(_hostSessionDir, "play", "check", "H8", playableWord, "H");
		check.ExitCode.Should().Be(0);
		check.StdOut.Should().Contain("Coup valide");

		var move = await RunCliAsync(_hostSessionDir, "play", "move", "H8", playableWord, "H");
		move.ExitCode.Should().Be(0);
		move.StdOut.Should().Contain("(online)");

		var challenge = await RunCliAsync(_guestSessionDir, "play", "challenge");
		challenge.ExitCode.Should().Be(0);
		challenge.StdOut.Should().Contain("Challenge");

		var swap = await RunCliAsync(_hostSessionDir, "play", "swap", "--all");
		swap.ExitCode.Should().Be(0);
		swap.StdOut.Should().Contain("Echange effectue (online)");
	}

	[Fact]
	public async Task Cli_Online_Challenge_WithoutPlayableMove_ReturnsError()
	{
		var create = await RunCliAsync(_hostSessionDir, "game", "create", "Alice", "--level", "casual");
		create.ExitCode.Should().Be(0);

		var challenge = await RunCliAsync(_hostSessionDir, "play", "challenge");
		challenge.ExitCode.Should().NotBe(0);
		challenge.StdErr.Should().Contain("Aucun dernier coup contestable");
	}

	[Fact]
	public async Task Cli_Online_MultiLobby_PrivateStartFlow_Works()
	{
		var create = await RunCliAsync(
			_hostSessionDir,
			"game", "create", "Alice",
			"--mode", "multi",
			"--name", "LobbyPriv",
			"--max-players", "4",
			"--with-ai",
			"--private",
			"--password", "secret42");

		create.ExitCode.Should().Be(0);
		create.StdOut.Should().Contain("Type      : multi");

		var gameId = ExtractGameId(create.StdOut);

		var joinWrongPassword = await RunCliAsync(
			_guestSessionDir,
			"game", "join", "Bob",
			"--game-id", gameId,
			"--password", "bad-secret");
		joinWrongPassword.ExitCode.Should().NotBe(0);
		joinWrongPassword.StdErr.Should().Contain("invalid game password");

		var joinOk = await RunCliAsync(
			_guestSessionDir,
			"game", "join", "Bob",
			"--game-id", gameId,
			"--password", "secret42");
		joinOk.ExitCode.Should().Be(0);
		joinOk.StdOut.Should().Contain("rejoint la partie online");

		var passBeforeStart = await RunCliAsync(_hostSessionDir, "play", "pass");
		passBeforeStart.ExitCode.Should().NotBe(0);
		passBeforeStart.StdErr.Should().Contain("game has not started yet");

		var start = await RunCliAsync(_hostSessionDir, "game", "start");
		start.ExitCode.Should().Be(0);
		start.StdOut.Should().Contain("Partie démarrée");

		var passAfterStart = await RunCliAsync(_hostSessionDir, "play", "pass");
		passAfterStart.ExitCode.Should().Be(0);
		passAfterStart.StdOut.Should().Contain("(online)");
	}

	[Fact]
	public async Task Cli_Online_PlayerCannotParticipateInTwoActiveGames_SameTime()
	{
		var create1 = await RunCliAsync(_hostSessionDir, "game", "create", "Alice", "--level", "casual");
		create1.ExitCode.Should().Be(0);

		var create2 = await RunCliAsync(_hostSessionDir, "game", "create", "Alice", "--level", "casual");
		create2.ExitCode.Should().NotBe(0);
		create2.StdErr.Should().Contain("player already active in game");

		var end = await RunCliAsync(_hostSessionDir, "game", "end");
		end.ExitCode.Should().Be(0);

		var create3 = await RunCliAsync(_hostSessionDir, "game", "create", "Alice", "--level", "casual");
		create3.ExitCode.Should().Be(0);
	}

	private async Task<string> GetPlayableWordFromRackAsync(string gameId)
	{
		var show = await RunCliAsync(_hostSessionDir, "game", "show", gameId, "--output", "json");
		show.ExitCode.Should().Be(0);

		using var snapshotDoc = JsonDocument.Parse(show.StdOut);
		var players = snapshotDoc.RootElement.GetProperty("Players");
		var rack = players[0].GetProperty("Rack");

		var letters = new string(
			rack.EnumerateArray()
				.Select(item => item.GetString())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => char.ToUpperInvariant(s![0]))
				.Where(char.IsLetter)
				.ToArray());

		var anagram = await RunCliAsync(_hostSessionDir, "dict", "anagram", letters, "--min-length", "2", "--output", "json");
		anagram.ExitCode.Should().Be(0);

		using var anagramDoc = JsonDocument.Parse(anagram.StdOut);
		var first = anagramDoc.RootElement.EnumerateArray().Select(x => x.GetString()).FirstOrDefault();

		first.Should().NotBeNullOrWhiteSpace("un mot jouable est requis pour valider le flux online");
		return first!;
	}

	private async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string sessionDir, params string[] args)
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

		psi.Environment["LAMA_RUNTIME_MODE"] = "online";
		psi.Environment["LAMA_SERVER_URL"] = _serverUrl;
		psi.Environment["LAMA_SESSION_DIR"] = sessionDir;

		using var process = Process.Start(psi)!;
		var stdOutTask = process.StandardOutput.ReadToEndAsync();
		var stdErrTask = process.StandardError.ReadToEndAsync();

		await process.WaitForExitAsync();
		var stdOut = await stdOutTask;
		var stdErr = await stdErrTask;

		return (process.ExitCode, stdOut, stdErr);
	}

	private async Task WaitForServerHealthAsync()
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);

		while (DateTime.UtcNow < deadline)
		{
			try
			{
				var response = await _httpClient.GetAsync($"{_serverUrl}/health");
				if (response.IsSuccessStatusCode)
					return;
			}
			catch
			{
				// retry
			}

			if (_serverProcess is { HasExited: true })
				break;

			await Task.Delay(200);
		}

		throw new InvalidOperationException(
			$"Lama.Server non disponible sur {_serverUrl}.\nSTDOUT:\n{_serverStdOut}\nSTDERR:\n{_serverStdErr}");
	}

	private static string ExtractGameId(string output)
	{
		var match = System.Text.RegularExpressions.Regex.Match(output, "([a-f0-9]{32})");
		if (!match.Success)
			throw new InvalidOperationException($"GameId introuvable dans la sortie: {output}");

		return match.Value;
	}

	private static int GetFreePort()
	{
		var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
		listener.Start();
		var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
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

