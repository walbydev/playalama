using FluentAssertions;
using Lama.Console.Services;
using Lama.Contracts;
using Lama.Infrastructure.Session;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Console.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="CommandContextParser"/>.
/// Vérifie le parsing des arguments CLI et la fusion avec la session persistée.
/// </summary>
public class CommandContextParserTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ISessionService _sessionService;

    public CommandContextParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LamaParserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir);
        _sessionService = new SessionService(NullLogger<SessionService>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Parsing de base

    [Fact]
    public void Parse_ReturnsNull_WhenLessThanTwoArgs()
    {
        var result = CommandContextParser.Parse([], _sessionService);
        result.Should().BeNull();

        result = CommandContextParser.Parse(["game"], _sessionService);
        result.Should().BeNull(because: "il faut au moins groupe + action");
    }

    [Theory]
    [InlineData("game",   "create")]
    [InlineData("play",   "move")]
    [InlineData("show",   "board")]
    [InlineData("GAME",   "CREATE")] // majuscules normalisées
    public void Parse_ExtractsGroupAndAction(string group, string action)
    {
        var result = CommandContextParser.Parse([group, action], _sessionService);

        result.Should().NotBeNull();
        result!.Group.Should().Be(group.ToLowerInvariant());
        result.Action.Should().Be(action.ToLowerInvariant());
    }

    [Fact]
    public void Parse_ExtractsPositionalArguments()
    {
        var args   = new[] { "play", "move", "H8", "MAISON", "H" };
        var result = CommandContextParser.Parse(args, _sessionService);

        result!.Arguments.Should().HaveCount(3);
        result.GetArgument(0).Should().Be("H8");
        result.GetArgument(1).Should().Be("MAISON");
        result.GetArgument(2).Should().Be("H");
    }

    [Fact]
    public void Parse_ExtractsLongOptions_WithValue()
    {
        var args   = new[] { "game", "create", "--level", "casual", "--players", "3" };
        var result = CommandContextParser.Parse(args, _sessionService);

        result!.GetOption("level").Should().Be("casual");
        result.GetOption("players").Should().Be("3");
    }

    [Fact]
    public void Parse_ExtractsLongOptions_BooleanFlag()
    {
        var args   = new[] { "play", "move", "H8", "MAISON", "H", "--dry-run" };
        var result = CommandContextParser.Parse(args, _sessionService);

        result!.HasOption("dry-run").Should().BeTrue();
        result.GetOption("dry-run").Should().BeNull(because: "un flag booléen n'a pas de valeur");
    }

    [Fact]
    public void Parse_ExtractsShortOptions_WithValue()
    {
        var args   = new[] { "show", "board", "-o", "json" };
        var result = CommandContextParser.Parse(args, _sessionService);

        result!.GetOption("o").Should().Be("json");
    }

    [Fact]
    public void Parse_ExtractsMixedArgsAndOptions()
    {
        var args = new[] { "play", "move", "H8", "--joker", "3=I", "MAISON", "H", "--dry-run" };
        var result = CommandContextParser.Parse(args, _sessionService);

        result!.Arguments.Should().BeEquivalentTo(["H8", "MAISON", "H"],
            because: "les arguments positionnels ne doivent pas inclure les options");
        result.GetOption("joker").Should().Be("3=I");
        result.HasOption("dry-run").Should().BeTrue();
    }

    #endregion

    #region Fusion avec la session

    [Fact]
    public void Parse_LoadsSessionContext_WhenSessionExists()
    {
        // Arrange — session active
        _sessionService.SaveSession(new SessionContext(
            GameId:        "game-abc",
            PlayerId:      "player-xyz",
            PlayerName:    "bob",
            Role:          Role.Host,
            GameLevel:     GameLevel.Standard,
            AuthToken:     null,
            TokenExpiresAt: null,
            CreatedAt:     DateTimeOffset.UtcNow,
            UpdatedAt:     DateTimeOffset.UtcNow));

        var result = CommandContextParser.Parse(["play", "move"], _sessionService);

        result!.GameId.Should().Be("game-abc",
            because: "le GameId doit être chargé depuis la session");
        result.PlayerId.Should().Be("player-xyz");
        result.PlayerName.Should().Be("bob");
        result.Role.Should().Be(Role.Host);
        result.GameLevel.Should().Be(GameLevel.Standard);
    }

    [Fact]
    public void Parse_UsesDefaultRole_WhenNoSession()
    {
        var result = CommandContextParser.Parse(["game", "list"], _sessionService);

        result!.Role.Should().Be(Role.Player,
            because: "sans session, le rôle par défaut est Player");
        result.GameId.Should().BeNull();
        result.PlayerId.Should().BeNull();
        result.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void Parse_CliOption_GameId_OverridesSession()
    {
        // Session avec game-abc
        _sessionService.SaveSession(new SessionContext(
            GameId:        "game-abc",
            PlayerId:      "player-xyz",
            PlayerName:    "bob",
            Role:          Role.Player,
            GameLevel:     GameLevel.Standard,
            AuthToken:     null,
            TokenExpiresAt: null,
            CreatedAt:     DateTimeOffset.UtcNow,
            UpdatedAt:     DateTimeOffset.UtcNow));

        // CLI surcharge avec --game-id
        var result = CommandContextParser.Parse(
            ["play", "move", "--game-id", "game-override"],
            _sessionService);

        result!.GameId.Should().Be("game-override",
            because: "--game-id en CLI doit surcharger la session");
    }

    [Fact]
    public void Parse_CliOption_Player_OverridesSession()
    {
        _sessionService.SaveSession(new SessionContext(
            GameId:        "game-abc",
            PlayerId:      "player-original",
            PlayerName:    "bob",
            Role:          Role.Player,
            GameLevel:     GameLevel.Casual,
            AuthToken:     null,
            TokenExpiresAt: null,
            CreatedAt:     DateTimeOffset.UtcNow,
            UpdatedAt:     DateTimeOffset.UtcNow));

        var result = CommandContextParser.Parse(
            ["play", "pass", "--player", "player-override"],
            _sessionService);

        result!.PlayerId.Should().Be("player-override",
            because: "--player en CLI doit surcharger la session");
    }

    [Fact]
    public void Parse_Role_ComesFromSession_NotFromCli()
    {
        // Le rôle ne peut PAS être surchargé par CLI (sécurité)
        _sessionService.SaveSession(new SessionContext(
            GameId:        "game-abc",
            PlayerId:      "player-xyz",
            PlayerName:    "bob",
            Role:          Role.Host,
            GameLevel:     GameLevel.Standard,
            AuthToken:     null,
            TokenExpiresAt: null,
            CreatedAt:     DateTimeOffset.UtcNow,
            UpdatedAt:     DateTimeOffset.UtcNow));

        var result = CommandContextParser.Parse(["show", "board"], _sessionService);

        result!.Role.Should().Be(Role.Host,
            because: "le rôle vient uniquement de la session, pas d'options CLI");
    }

    #endregion

    #region CommandId

    [Fact]
    public void Parse_CommandId_IsLowercaseGroupDotAction()
    {
        var result = CommandContextParser.Parse(["GAME", "CREATE"], _sessionService);

        result!.CommandId.Should().Be("game.create",
            because: "CommandId doit être en minuscules");
    }

    #endregion
}
