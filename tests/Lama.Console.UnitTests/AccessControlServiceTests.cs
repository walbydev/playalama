using FluentAssertions;
using Lama.Console.Services;
using Lama.Contracts;

namespace Lama.Console.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="AccessControlService"/>.
/// Vérifie la matrice de permissions complète : rôle × niveau de partie × commande.
///
/// Rôles : SuperAdmin, Admin, Host, Player, Spectator
/// Niveaux : Casual, Standard, Competitive, Tournament
/// </summary>
public class AccessControlServiceTests
{
    private readonly AccessControlService _sut = new();

    #region SuperAdmin

    [Theory]
    [InlineData("system.restart")]
    [InlineData("system.account.create")]
    [InlineData("system.account.list")]
    [InlineData("system.account.revoke")]
    [InlineData("game.create")]
    [InlineData("game.end.force")]
    [InlineData("dict.install")]
    [InlineData("show.hints")]
    [InlineData("show.board")]
    [InlineData("play.simulate")]
    public void SuperAdmin_CanAccessAllNonPlayCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.SuperAdmin);

        result.IsAllowed.Should().BeTrue(
            because: $"SuperAdmin doit avoir accès à '{command}'");
    }

    [Theory]
    [InlineData("system.account.create", GameLevel.Competitive)]
    [InlineData("game.create",           GameLevel.Tournament)]
    [InlineData("show.hints",            GameLevel.Standard)]
    public void SuperAdmin_CanAccessAllCommands_RegardlessOfGameLevel(string command, GameLevel level)
    {
        var result = _sut.CheckAccess(command, Role.SuperAdmin, level);

        result.IsAllowed.Should().BeTrue(
            because: $"SuperAdmin doit avoir accès à '{command}' en mode {level}");
    }

    #endregion

    #region Admin

    [Theory]
    [InlineData("system.restart")]
    [InlineData("system.logs")]
    [InlineData("game.create")]
    [InlineData("game.end.force")]
    [InlineData("dict.install")]
    [InlineData("show.hints")]
    [InlineData("play.simulate")]
    [InlineData("show.board")]
    [InlineData("game.list")]
    public void Admin_CanAccessAdminAndManagementCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Admin);

        result.IsAllowed.Should().BeTrue(
            because: $"Admin doit avoir accès à '{command}'");
    }

    [Theory]
    [InlineData("play.move")]
    [InlineData("play.pass")]
    [InlineData("play.swap")]
    [InlineData("play.challenge")]
    [InlineData("show.rack")]
    public void Admin_CannotPlayCommands_AntiCheatProtection(string command)
    {
        var result = _sut.CheckAccess(command, Role.Admin);

        result.IsAllowed.Should().BeFalse(
            because: $"Admin ne peut pas jouer — protection anti-triche (commande '{command}')");
        result.Reason.Should().NotBeNullOrEmpty(
            because: "un refus doit toujours avoir une raison explicite");
    }

    [Theory]
    [InlineData("system.account.create")]
    [InlineData("system.account.list")]
    [InlineData("system.account.revoke")]
    [InlineData("system.account.reset-password")]
    public void Admin_CannotAccessSuperAdminOnlyCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Admin);

        result.IsAllowed.Should().BeFalse(
            because: $"'{command}' est réservé au SuperAdmin");
    }

    [Theory]
    [InlineData("system.restart", GameLevel.Competitive)]
    [InlineData("game.create",    GameLevel.Tournament)]
    [InlineData("show.hints",     GameLevel.Standard)]
    [InlineData("dict.check",     GameLevel.Competitive)]
    public void Admin_CanAccessManagementCommands_RegardlessOfGameLevel(string command, GameLevel level)
    {
        var result = _sut.CheckAccess(command, Role.Admin, level);

        result.IsAllowed.Should().BeTrue(
            because: $"Admin doit avoir accès à '{command}' peu importe le GameLevel");
    }

    #endregion

    #region Host

    [Theory]
    [InlineData("play.move")]
    [InlineData("play.pass")]
    [InlineData("play.swap")]
    [InlineData("play.challenge")]
    [InlineData("show.rack")]
    public void Host_CanPlayCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Host, GameLevel.Standard);

        result.IsAllowed.Should().BeTrue(
            because: $"Host joue comme un Player, '{command}' doit être accessible");
    }

    [Theory]
    [InlineData("game.create")]
    [InlineData("game.end.force")]
    [InlineData("game.kick")]
    public void Host_CanManageHisOwnGame(string command)
    {
        var result = _sut.CheckAccess(command, Role.Host);

        result.IsAllowed.Should().BeTrue(
            because: $"Host peut gérer sa propre partie ('{command}')");
    }

    [Theory]
    [InlineData("system.restart")]
    [InlineData("system.logs")]
    [InlineData("system.account.create")]
    public void Host_CannotAccessSystemCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Host);

        result.IsAllowed.Should().BeFalse(
            because: $"Host n'a pas accès aux commandes système ('{command}')");
    }


    [Theory]
    [InlineData("game.end.force")]
    public void Player_CannotManageGame_OnlyHostCan(string command)
    {
        var result = _sut.CheckAccess(command, Role.Player);

        result.IsAllowed.Should().BeFalse(
            because: $"'{command}' est réservée à l'hôte de la partie");
        result.Reason.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Player — commandes de jeu

    [Theory]
    [InlineData("play.move")]
    [InlineData("play.pass")]
    [InlineData("play.swap")]
    [InlineData("play.challenge")]
    [InlineData("show.rack")]
    [InlineData("game.join")]
    [InlineData("game.pause")]
    [InlineData("game.save")]
    public void Player_CanAccessGameCommands_InAllLevels(string command)
    {
        foreach (var level in Enum.GetValues<GameLevel>())
        {
            var result = _sut.CheckAccess(command, Role.Player, level);
            result.IsAllowed.Should().BeTrue(
                because: $"Player doit pouvoir exécuter '{command}' en {level}");
        }
    }

    [Theory]
    [InlineData("system.restart")]
    [InlineData("game.end.force")]
    [InlineData("dict.install")]
    [InlineData("dict.remove")]
    public void Player_CannotAccessAdminCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Player, GameLevel.Casual);

        result.IsAllowed.Should().BeFalse(
            because: $"'{command}' est réservée aux admins");
        result.Reason.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Spectator

    [Theory]
    [InlineData("game.list")]
    [InlineData("game.show")]
    [InlineData("show.board")]
    [InlineData("show.scores")]
    [InlineData("show.history")]
    [InlineData("tournament.list")]
    [InlineData("tournament.standings")]
    public void Spectator_CanAccessReadOnlyCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Spectator);

        result.IsAllowed.Should().BeTrue(
            because: $"'{command}' est en lecture seule et accessible au Spectator");
    }

    [Theory]
    [InlineData("play.move")]
    [InlineData("play.pass")]
    [InlineData("show.rack")]
    [InlineData("game.save")]
    [InlineData("system.restart")]
    public void Spectator_CannotAccessActionCommands(string command)
    {
        var result = _sut.CheckAccess(command, Role.Spectator);

        result.IsAllowed.Should().BeFalse(
            because: $"Spectator ne peut pas exécuter '{command}'");
    }

    #endregion

    #region Commandes publiques

    [Fact]
    public void SystemSetup_IsAccessible_WithoutAuthentication()
    {
        // system.setup est public — accessible sans rôle/token (premier lancement)
        // La commande elle-même est son propre garde-fou (refuse si déjà initialisé)
        foreach (var role in Enum.GetValues<Role>())
        {
            var result = _sut.CheckAccess("system.setup", role);
            result.IsAllowed.Should().BeTrue(
                because: $"system.setup doit être accessible quel que soit le rôle ({role})");
        }
    }

    [Theory]
    [InlineData("login")]
    [InlineData("logout")]
    public void AuthCommands_AreAccessible_WithoutAuthentication(string command)
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            var result = _sut.CheckAccess(command, role);
            result.IsAllowed.Should().BeTrue(
                because: $"{command} doit être accessible sans session ({role})");
        }
    }

    [Theory]
    [InlineData("game.create")]
    [InlineData("game.join")]
    [InlineData("game.list")]
    public void PublicGameCommands_AreAccessible_RegardlessOfRole(string command)
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            var result = _sut.CheckAccess(command, role);
            result.IsAllowed.Should().BeTrue(
                because: $"{command} est une commande publique ({role})");
        }
    }

    [Theory]
    [InlineData("system.server.show")]
    [InlineData("system.server.clear")]
    public void RuntimeServerCommands_AreAccessible_RegardlessOfRole(string command)
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            var result = _sut.CheckAccess(command, role);
            result.IsAllowed.Should().BeTrue(
                because: $"{command} doit être accessible quel que soit le rôle ({role})");
        }
    }

    #endregion

    #region GetAllowedCommands

    [Fact]
    public void GetAllowedCommands_SuperAdmin_ContainsAllSets()
    {
        var allowed = _sut.GetAllowedCommands(Role.SuperAdmin);

        allowed.Should().Contain("system.account.create",  because: "SuperAdmin gère les comptes");
        allowed.Should().Contain("system.restart",         because: "SuperAdmin accède au système");
        allowed.Should().Contain("game.create",            because: "SuperAdmin peut créer une partie");
        allowed.Should().Contain("game.list",              because: "SuperAdmin voit toutes les parties");
        allowed.Should().Contain("system.setup",           because: "system.setup est public");
    }

    [Fact]
    public void GetAllowedCommands_Admin_ContainsSystemAndManagement_ButNotPlay()
    {
        var allowed = _sut.GetAllowedCommands(Role.Admin);

        allowed.Should().Contain("system.restart",  because: "Admin accède au système");
        allowed.Should().Contain("show.hints",      because: "Admin peut utiliser les aides");
        allowed.Should().Contain("game.list",       because: "Admin voit les parties");
        allowed.Should().Contain("game.create",     because: "Admin peut créer une partie");

        allowed.Should().NotContain("play.move",    because: "Admin ne joue pas (anti-triche)");
        allowed.Should().NotContain("play.pass",    because: "Admin ne joue pas (anti-triche)");
        allowed.Should().NotContain("show.rack",    because: "Admin ne peut pas voir les racks");
        allowed.Should().NotContain("system.account.create",
            because: "system.account.* est réservé au SuperAdmin");
    }

    [Fact]
    public void GetAllowedCommands_Host_ContainsPlayAndManagement_ButNotSystem()
    {
        var allowed = _sut.GetAllowedCommands(Role.Host, GameLevel.Standard);

        allowed.Should().Contain("play.move",       because: "Host joue");
        allowed.Should().Contain("play.pass",       because: "Host joue");
        allowed.Should().Contain("show.rack",       because: "Host voit son rack");
        allowed.Should().Contain("game.end.force",  because: "Host peut forcer la fin");
        allowed.Should().Contain("game.create",     because: "Host peut créer une partie");

        allowed.Should().NotContain("system.restart",           because: "Host n'a pas accès au système");
        allowed.Should().NotContain("system.account.create",    because: "réservé SuperAdmin");
        // Aides désactivées en Standard
        allowed.Should().NotContain("show.hints",   because: "aides désactivées en Standard");
    }

    [Fact]
    public void GetAllowedCommands_PlayerCasual_ContainsAids()
    {
        var allowed = _sut.GetAllowedCommands(Role.Player, GameLevel.Casual);

        allowed.Should().Contain("show.hints",      because: "aide activée en Casual");
        allowed.Should().Contain("dict.check",      because: "aide activée en Casual");
        allowed.Should().Contain("play.simulate",   because: "aide activée en Casual");
        allowed.Should().Contain("play.suggest",    because: "aide activée en Casual");
        allowed.Should().Contain("play.move",       because: "Player peut toujours jouer");
        allowed.Should().Contain("game.create",     because: "commande publique");

        allowed.Should().NotContain("system.restart",   because: "Player n'a pas accès au système");
    }

    [Fact]
    public void GetAllowedCommands_PlayerCompetitive_ExcludesAids()
    {
        var allowed = _sut.GetAllowedCommands(Role.Player, GameLevel.Competitive);

        allowed.Should().NotContain("show.hints",       because: "aides désactivées en Competitive");
        allowed.Should().NotContain("dict.check",       because: "aides désactivées en Competitive");
        allowed.Should().NotContain("play.simulate",    because: "aides désactivées en Competitive");
        allowed.Should().NotContain("play.suggest",     because: "aides désactivées en Competitive");

        allowed.Should().Contain("play.move",           because: "Player joue toujours");
        allowed.Should().Contain("play.challenge",      because: "challenge toujours disponible");
    }

    [Fact]
    public void GetAllowedCommands_Spectator_OnlyContainsReadOnlyCommands()
    {
        var allowed = _sut.GetAllowedCommands(Role.Spectator);

        allowed.Should().Contain("show.board",      because: "Spectator voit le plateau");
        allowed.Should().Contain("game.list",       because: "Spectator voit les parties");

        allowed.Should().NotContain("play.move",    because: "Spectator ne joue pas");
        allowed.Should().NotContain("show.hints",   because: "Spectator ne peut pas utiliser les aides");
        allowed.Should().NotContain("system.restart", because: "Spectator n'a pas accès au système");
    }

    [Fact]
    public void GetAllowedCommands_AllRoles_ContainSystemSetup()
    {
        // system.setup est public — dans la liste pour tous les rôles
        foreach (var role in Enum.GetValues<Role>())
        {
            var allowed = _sut.GetAllowedCommands(role);
            allowed.Should().Contain("system.setup",
                because: $"system.setup est public et doit apparaître pour le rôle {role}");
        }
    }

    #endregion

    #region Cas limites

    [Fact]
    public void CheckAccess_EmptyCommand_ReturnsDenied_WithReason()
    {
        var result = _sut.CheckAccess("", Role.Admin);

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty(
            because: "un refus doit toujours inclure une raison");
    }

    [Fact]
    public void CheckAccess_WhitespaceCommand_ReturnsDenied()
    {
        var result = _sut.CheckAccess("   ", Role.SuperAdmin);

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void CheckAccess_UnknownCommand_ReturnsDenied()
    {
        var result = _sut.CheckAccess("unknown.command.xyz", Role.Player);

        result.IsAllowed.Should().BeFalse(
            because: "les commandes inconnues sont refusées par défaut (fail-safe)");
    }

    [Fact]
    public void CheckAccess_CommandWithAdminPrefix_IsDeniedToPlayer()
    {
        // "system.config.show" doit être bloqué car il commence par "system.config"
        var result = _sut.CheckAccess("system.config.show", Role.Player);

        result.IsAllowed.Should().BeFalse(
            because: "le préfixe admin est bloqué même pour des sous-commandes");
    }

    [Fact]
    public void CheckAccess_CommandIsCaseInsensitive()
    {
        var lower  = _sut.CheckAccess("show.board", Role.Player);
        var upper  = _sut.CheckAccess("SHOW.BOARD", Role.Player);
        var mixed  = _sut.CheckAccess("Show.Board", Role.Player);

        lower.IsAllowed.Should().BeTrue();
        upper.IsAllowed.Should().Be(lower.IsAllowed,
            because: "la comparaison des commandes doit être insensible à la casse");
        mixed.IsAllowed.Should().Be(lower.IsAllowed,
            because: "la comparaison des commandes doit être insensible à la casse");
    }

    [Fact]
    public void AccessResult_Allowed_HasCorrectProperties()
    {
        var result = AccessResult.Allowed;

        result.IsAllowed.Should().BeTrue();
        result.Reason.Should().BeNull(
            because: "un accès autorisé n'a pas de raison de refus");
    }

    [Fact]
    public void AccessResult_Denied_HasCorrectProperties()
    {
        const string reason = "accès refusé pour test";
        var result = AccessResult.Denied(reason);

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Be(reason);
    }

    #endregion
}
