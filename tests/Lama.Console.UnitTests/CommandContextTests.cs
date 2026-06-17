using FluentAssertions;
using Lama.Console.Services;
using Lama.Contracts;

namespace Lama.Console.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="CommandContext"/>.
/// Vérifie la construction du contexte et les propriétés calculées.
/// </summary>
public class CommandContextTests
{
    #region CommandId

    [Theory]
    [InlineData("game",   "create",    "game.create")]
    [InlineData("play",   "move",      "play.move")]
    [InlineData("show",   "board",     "show.board")]
    [InlineData("dict",   "check",     "dict.check")]
    [InlineData("system", "status",    "system.status")]
    public void CommandId_CombinesGroupAndAction_WithDot(string group, string action, string expected)
    {
        var context = new CommandContext { Group = group, Action = action };

        context.CommandId.Should().Be(expected);
    }

    [Fact]
    public void CommandId_UsesExplicitValue_WhenProvided()
    {
        var context = new CommandContext
        {
            Group     = "system",
            Action    = "account",
            CommandId = "system.account.create"
        };

        context.CommandId.Should().Be("system.account.create");
    }

    [Fact]
    public void CommandId_SupportsSingleLevelCommand_WhenExplicitlySet()
    {
        var context = new CommandContext { CommandId = "login" };

        context.CommandId.Should().Be("login");
    }

    #endregion

    #region Options globales — propriétés calculées

    [Fact]
    public void Verbose_IsTrue_WhenVerboseOptionPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["verbose"] = null }
        };

        context.Verbose.Should().BeTrue();
    }

    [Fact]
    public void Verbose_IsTrue_WhenVFlagPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["V"] = null }
        };

        context.Verbose.Should().BeTrue();
    }

    [Fact]
    public void Verbose_IsFalse_WhenNotPresent()
    {
        var context = new CommandContext();

        context.Verbose.Should().BeFalse();
    }

    [Fact]
    public void Quiet_IsTrue_WhenQuietOptionPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["quiet"] = null }
        };

        context.Quiet.Should().BeTrue();
    }

    [Fact]
    public void NoColor_IsTrue_WhenNoColorPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["no-color"] = null }
        };

        context.NoColor.Should().BeTrue();
    }

    [Fact]
    public void HighContrast_IsTrue_WhenHighContrastPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["high-contrast"] = null }
        };

        context.HighContrast.Should().BeTrue();
    }

    [Theory]
    [InlineData("lang",  "fr", "fr")]
    [InlineData("lang",  "en", "en")]
    [InlineData("l",     "de", "de")]
    public void Lang_ReturnsOptionValue_WhenPresent(string key, string value, string expected)
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { [key] = value }
        };

        context.Lang.Should().Be(expected);
    }

    [Fact]
    public void Lang_DefaultsToFr_WhenNotPresent()
    {
        var context = new CommandContext();

        context.Lang.Should().Be("fr",
            because: "la langue par défaut est le français");
    }

    [Theory]
    [InlineData("output", "json",  "json")]
    [InlineData("output", "csv",   "csv")]
    [InlineData("output", "text",  "text")]
    [InlineData("o",      "json",  "json")]
    public void OutputFormat_ReturnsOptionValue_WhenPresent(string key, string value, string expected)
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { [key] = value }
        };

        context.OutputFormat.Should().Be(expected);
    }

    [Fact]
    public void OutputFormat_DefaultsToText_WhenNotPresent()
    {
        var context = new CommandContext();

        context.OutputFormat.Should().Be("text",
            because: "le format de sortie par défaut est text");
    }

    #endregion

    #region GetArgument

    [Fact]
    public void GetArgument_ReturnsValue_WhenIndexValid()
    {
        var context = new CommandContext
        {
            Arguments = ["H8", "MAISON", "H"]
        };

        context.GetArgument(0).Should().Be("H8");
        context.GetArgument(1).Should().Be("MAISON");
        context.GetArgument(2).Should().Be("H");
    }

    [Fact]
    public void GetArgument_ReturnsNull_WhenIndexOutOfRange()
    {
        var context = new CommandContext { Arguments = ["H8"] };

        context.GetArgument(5).Should().BeNull(
            because: "un index hors limite doit retourner null, pas d'exception");
    }

    [Fact]
    public void GetArgument_ReturnsNull_WhenNoArguments()
    {
        var context = new CommandContext();

        context.GetArgument(0).Should().BeNull();
    }

    #endregion

    #region HasOption / GetOption

    [Fact]
    public void HasOption_ReturnsTrue_WhenOptionPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["dry-run"] = null }
        };

        context.HasOption("dry-run").Should().BeTrue();
    }

    [Fact]
    public void HasOption_ReturnsFalse_WhenOptionAbsent()
    {
        var context = new CommandContext();

        context.HasOption("dry-run").Should().BeFalse();
    }

    [Fact]
    public void GetOption_ReturnsValue_WhenOptionPresent()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["joker"] = "3=I" }
        };

        context.GetOption("joker").Should().Be("3=I");
    }

    [Fact]
    public void GetOption_ReturnsNull_WhenOptionAbsent()
    {
        var context = new CommandContext();

        context.GetOption("joker").Should().BeNull();
    }

    [Fact]
    public void GetOption_ReturnsNull_ForBooleanFlag()
    {
        var context = new CommandContext
        {
            Options = new Dictionary<string, string?> { ["dry-run"] = null }
        };

        context.GetOption("dry-run").Should().BeNull(
            because: "un flag booléen n'a pas de valeur associée");
    }

    #endregion

    #region HasActiveSession

    [Fact]
    public void HasActiveSession_IsTrue_WhenGameIdAndPlayerIdSet()
    {
        var context = new CommandContext
        {
            GameId   = "game123",
            PlayerId = "player456"
        };

        context.HasActiveSession.Should().BeTrue();
    }

    [Fact]
    public void HasActiveSession_IsFalse_WhenGameIdNull()
    {
        var context = new CommandContext { PlayerId = "player456" };

        context.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void HasActiveSession_IsFalse_WhenPlayerIdNull()
    {
        var context = new CommandContext { GameId = "game123" };

        context.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void HasActiveSession_IsFalse_ByDefault()
    {
        var context = new CommandContext();

        context.HasActiveSession.Should().BeFalse();
    }

    #endregion

    #region Role et GameLevel par défaut

    [Fact]
    public void Role_DefaultsToPlayer()
    {
        var context = new CommandContext();

        context.Role.Should().Be(Role.Player,
            because: "le rôle par défaut est Player");
    }

    [Fact]
    public void GameLevel_DefaultsToNull()
    {
        var context = new CommandContext();

        context.GameLevel.Should().BeNull(
            because: "GameLevel est null en dehors d'une partie");
    }

    #endregion
}
