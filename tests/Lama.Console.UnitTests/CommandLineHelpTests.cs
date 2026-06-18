using FluentAssertions;
using Lama.Console.Commands.Middleware;
using Lama.Console.Modes;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Console.UnitTests;

public sealed class CommandLineHelpTests
{
    [Fact]
    public async Task HelpCommand_PrintsGlobalHelp()
    {
        var mode = CreateMode(["help"]);

        var (stdout, stderr, exitCode) = await CaptureAsync(() => mode.RunAsync());

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("lama help [groupe] [action]");
    }

    [Fact]
    public async Task GroupHelp_PrintsSystemHelp()
    {
        var mode = CreateMode(["system", "--help"]);

        var (stdout, stderr, exitCode) = await CaptureAsync(() => mode.RunAsync());

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Administration systeme");
        stdout.Should().Contain("status");
        stdout.Should().Contain("restart");
    }

    [Fact]
    public async Task CommandHelp_SystemRestart_ExplainsLogicalRestart()
    {
        var mode = CreateMode(["system", "restart", "--help"]);

        var (stdout, stderr, exitCode) = await CaptureAsync(() => mode.RunAsync());

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Redemarrage logique in-process");
        stdout.Should().Contain("ACL");
        stdout.Should().Contain("Formats sortie");
        stdout.Should().Contain("Ne redemarre pas un service OS externe");
    }

    [Fact]
    public async Task CommandHelp_SystemAccountCreate_WorksWithThreeLevelPath()
    {
        var mode = CreateMode(["system", "account", "create", "--help"]);

        var (stdout, stderr, exitCode) = await CaptureAsync(() => mode.RunAsync());

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("lama system account create <username>");
        stdout.Should().Contain("ACL");
        stdout.Should().Contain("SuperAdmin");
    }

    [Fact]
    public async Task UnknownGroupHelp_ReturnsInvalidArgument()
    {
        var mode = CreateMode(["unknown", "--help"]);

        var (stdout, stderr, exitCode) = await CaptureAsync(() => mode.RunAsync());

        exitCode.Should().Be(ExitCodes.InvalidArgument);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("Groupe inconnu");
    }

    [Fact]
    public async Task SingleLevelHelp_Login_Works()
    {
        var mode = CreateMode(["login", "--help"]);

        var (stdout, stderr, exitCode) = await CaptureAsync(() => mode.RunAsync());

        exitCode.Should().Be(ExitCodes.Success);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("Usage : lama login");
    }

    private static CommandLineMode CreateMode(string[] args)
    {
        var middleware = new AccessControlMiddleware(new AccessControlService());
        return new CommandLineMode(
            args,
            new NoOpDispatcher(),
            middleware,
            new InMemorySessionService(),
            NullLogger<CommandLineMode>.Instance);
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

    private sealed class NoOpDispatcher : ICommandDispatcher
    {
        public Task<int> DispatchAsync(CommandContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExitCodes.Success);
    }

    private sealed class InMemorySessionService : ISessionService
    {
        public SessionContext? LoadSession() => null;
        public void SaveSession(SessionContext session) { }
        public void ClearSession() { }
        public string SessionFilePath => "in-memory";
    }
}

