using Lama.Console.Services;
using Lama.Contracts;

namespace Lama.Console.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="AccessControlService"/>.
/// Vérifie la matrice de permissions : rôle × niveau de partie × commande.
/// </summary>
public class AccessControlServiceTests
{
    private readonly AccessControlService _sut = new();

    // -------------------------------------------------------------------------
    // Admin — accès universel
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("system.restart")]
    [InlineData("system.logs")]
    [InlineData("game.create")]
    [InlineData("game.end.force")]
    [InlineData("dict.install")]
    [InlineData("show.hints")]
    [InlineData("play.simulate")]
    [InlineData("play.move")]
    [InlineData("show.board")]
    public void Admin_CanAccessAllCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Admin);

        Assert.True(result.IsAllowed);
    }

    [Theory]
    [InlineData("system.restart", GameLevel.Competitive)]
    [InlineData("game.create", GameLevel.Tournament)]
    [InlineData("show.hints", GameLevel.Standard)]
    [InlineData("dict.check", GameLevel.Competitive)]
    public void Admin_CanAccessAllCommands_RegardlessOfGameLevel(string command, GameLevel level)
    {
        var result = _sut.CheckAccess(command, Role.Admin, level);

        Assert.True(result.IsAllowed);
    }

    // -------------------------------------------------------------------------
    // Commandes admin-only — refusées aux joueurs et spectateurs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("system.restart")]
    [InlineData("system.logs")]
    [InlineData("system.shutdown")]
    [InlineData("system.diagnostics")]
    [InlineData("game.create")]
    [InlineData("game.end.force")]
    [InlineData("dict.install")]
    [InlineData("dict.remove")]
    [InlineData("dict.add-word")]
    public void AdminOnlyCommands_AreDeniedToPlayer(string command)
    {
        var result = _sut.CheckAccess(command, Role.Player, GameLevel.Casual);

        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Reason);
    }

    [Theory]
    [InlineData("system.restart")]
    [InlineData("game.create")]
    [InlineData("dict.install")]
    public void AdminOnlyCommands_AreDeniedToSpectator(string command)
    {
        var result = _sut.CheckAccess(command, Role.Spectator);

        Assert.False(result.IsAllowed);
    }

    // -------------------------------------------------------------------------
    // Commandes d'aide (Casual uniquement)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("play.check")]
    [InlineData("play.simulate")]
    [InlineData("show.hints")]
    [InlineData("dict.check")]
    [InlineData("dict.search")]
    [InlineData("dict.anagram")]
    public void CasualCommands_AreAllowedInCasualMode(string command)
    {
        var result = _sut.CheckAccess(command, Role.Player, GameLevel.Casual);

        Assert.True(result.IsAllowed);
    }

    [Theory]
    [InlineData("play.check", GameLevel.Standard)]
    [InlineData("play.simulate", GameLevel.Standard)]
    [InlineData("show.hints", GameLevel.Standard)]
    [InlineData("dict.check", GameLevel.Competitive)]
    [InlineData("dict.search", GameLevel.Competitive)]
    [InlineData("show.hints", GameLevel.Competitive)]
    [InlineData("play.check", GameLevel.Tournament)]
    [InlineData("show.hints", GameLevel.Tournament)]
    public void CasualCommands_AreDeniedBeyondCasualMode(string command, GameLevel level)
    {
        var result = _sut.CheckAccess(command, Role.Player, level);

        Assert.False(result.IsAllowed);
        Assert.Contains("Casual", result.Reason);
    }

    [Theory]
    [InlineData("play.check")]
    [InlineData("show.hints")]
    [InlineData("dict.check")]
    public void CasualCommands_AreAllowedWhenNoGameLevelSet(string command)
    {
        // Hors contexte de partie, les aides sont accessibles par défaut
        var result = _sut.CheckAccess(command, Role.Player, gameLevel: null);

        Assert.True(result.IsAllowed);
    }

    // -------------------------------------------------------------------------
    // Commandes de jeu — accessibles à tous les joueurs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("play.move")]
    [InlineData("play.pass")]
    [InlineData("play.swap")]
    [InlineData("play.challenge")]
    [InlineData("show.rack")]
    [InlineData("game.join")]
    [InlineData("game.pause")]
    [InlineData("game.save")]
    public void PlayerCommands_AreAllowedForPlayer_InAllLevels(string command)
    {
        foreach (var level in Enum.GetValues<GameLevel>())
        {
            var result = _sut.CheckAccess(command, Role.Player, level);
            Assert.True(result.IsAllowed,
                $"Commande '{command}' devrait être autorisée en {level} pour un joueur.");
        }
    }

    // -------------------------------------------------------------------------
    // Commandes en lecture seule — accessibles aux spectateurs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("game.list")]
    [InlineData("game.show")]
    [InlineData("show.board")]
    [InlineData("show.scores")]
    [InlineData("show.history")]
    [InlineData("tournament.list")]
    [InlineData("tournament.standings")]
    public void ReadOnlyCommands_AreAllowedForSpectator(string command)
    {
        var result = _sut.CheckAccess(command, Role.Spectator);

        Assert.True(result.IsAllowed);
    }

    [Theory]
    [InlineData("play.move")]
    [InlineData("play.pass")]
    [InlineData("show.rack")]
    [InlineData("game.join")]
    [InlineData("game.save")]
    public void PlayerCommands_AreDeniedToSpectator(string command)
    {
        var result = _sut.CheckAccess(command, Role.Spectator);

        Assert.False(result.IsAllowed);
    }

    // -------------------------------------------------------------------------
    // GetAllowedCommands
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAllowedCommands_Admin_ContainsAllCommandSets()
    {
        var allowed = _sut.GetAllowedCommands(Role.Admin);

        Assert.Contains("system.restart", allowed);
        Assert.Contains("show.hints", allowed);
        Assert.Contains("play.move", allowed);
        Assert.Contains("game.list", allowed);
    }

    [Fact]
    public void GetAllowedCommands_PlayerCasual_ContainsAidCommands()
    {
        var allowed = _sut.GetAllowedCommands(Role.Player, GameLevel.Casual);

        Assert.Contains("show.hints", allowed);
        Assert.Contains("dict.check", allowed);
        Assert.Contains("play.simulate", allowed);
        Assert.DoesNotContain("system.restart", allowed);
    }

    [Fact]
    public void GetAllowedCommands_PlayerCompetitive_ExcludesAidCommands()
    {
        var allowed = _sut.GetAllowedCommands(Role.Player, GameLevel.Competitive);

        Assert.DoesNotContain("show.hints", allowed);
        Assert.DoesNotContain("dict.check", allowed);
        Assert.DoesNotContain("play.simulate", allowed);
        Assert.Contains("play.move", allowed);
    }

    [Fact]
    public void GetAllowedCommands_Spectator_OnlyContainsReadOnlyCommands()
    {
        var allowed = _sut.GetAllowedCommands(Role.Spectator);

        Assert.Contains("show.board", allowed);
        Assert.Contains("game.list", allowed);
        Assert.DoesNotContain("play.move", allowed);
        Assert.DoesNotContain("show.hints", allowed);
        Assert.DoesNotContain("system.restart", allowed);
    }

    // -------------------------------------------------------------------------
    // Cas limites
    // -------------------------------------------------------------------------

    [Fact]
    public void CheckAccess_EmptyCommand_ReturnsDenied()
    {
        var result = _sut.CheckAccess("", Role.Admin);

        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void CheckAccess_UnknownCommand_ReturnsDenied()
    {
        var result = _sut.CheckAccess("unknown.command", Role.Player);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void CheckAccess_CommandWithAdminPrefix_IsDeniedToPlayer()
    {
        // "system.config.show" doit être bloqué car préfixé par "system.config"
        var result = _sut.CheckAccess("system.config.show", Role.Player);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void AccessResult_Allowed_IsAllowedTrue()
    {
        Assert.True(AccessResult.Allowed.IsAllowed);
        Assert.Null(AccessResult.Allowed.Reason);
    }

    [Fact]
    public void AccessResult_Denied_IsAllowedFalse_WithReason()
    {
        var result = AccessResult.Denied("motif");

        Assert.False(result.IsAllowed);
        Assert.Equal("motif", result.Reason);
    }
}
