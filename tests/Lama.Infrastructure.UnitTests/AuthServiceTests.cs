using FluentAssertions;
using Lama.Contracts;
using Lama.Infrastructure.Auth;
using Lama.Infrastructure.Session;
using Lama.Infrastructure.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Infrastructure.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="AuthService"/>.
/// Vérifie le login, la validation de token et le logout.
/// </summary>
[Collection(Helpers.SequentialTestCollection.Name)]
public class AuthServiceTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly AccountService _accountService;
    private readonly SessionService _sessionService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tempDir = new TempDirectory();
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir.Path);

        _accountService = new AccountService(NullLogger<AccountService>.Instance);
        _sessionService = new SessionService(NullLogger<SessionService>.Instance);
        _sut = new AuthService(
            _accountService,
            _sessionService,
            NullLogger<AuthService>.Instance);

        // Créer un compte de test pour tous les tests
        _accountService.CreateSuperAdmin("superadmin", "SuperSecret123!");
        _accountService.CreateAdmin("alice",      "AliceSecret456!");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        _tempDir.Dispose();
    }

    #region Login

    [Fact]
    public void Login_Succeeds_WithCorrectCredentials_SuperAdmin()
    {
        var result = _sut.Login("superadmin", "SuperSecret123!");

        result.Success.Should().BeTrue();
        result.Account.Should().NotBeNull();
        result.Account!.Role.Should().Be(Role.SuperAdmin);
        result.Token.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Login_Succeeds_WithCorrectCredentials_Admin()
    {
        var result = _sut.Login("alice", "AliceSecret456!");

        result.Success.Should().BeTrue();
        result.Account!.Username.Should().Be("alice");
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Login_Fails_WithWrongPassword()
    {
        var result = _sut.Login("alice", "WrongPassword!");

        result.Success.Should().BeFalse();
        result.Token.Should().BeNull();
        result.Account.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Login_Fails_WithUnknownUsername()
    {
        var result = _sut.Login("unknown", "SomePassword!");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Login_Fails_ForRevokedAccount()
    {
        _accountService.Revoke("alice");

        var result = _sut.Login("alice", "AliceSecret456!");

        result.Success.Should().BeFalse(
            because: "un compte révoqué ne doit pas pouvoir se connecter");
    }

    [Fact]
    public void Login_IsCaseInsensitive_ForUsername()
    {
        var result = _sut.Login("ALICE", "AliceSecret456!");

        result.Success.Should().BeTrue(
            because: "le login est insensible à la casse pour le nom d'utilisateur");
    }

    [Fact]
    public void Login_ProducesValidToken()
    {
        var loginResult = _sut.Login("alice", "AliceSecret456!");

        var validation = _sut.ValidateToken(loginResult.Token!);

        validation.Valid.Should().BeTrue(
            because: "un token produit par Login doit être immédiatement valide");
        validation.Account!.Username.Should().Be("alice");
    }

    [Fact]
    public void Login_DoesNotRevealWhetherUsernameExists()
    {
        // Les deux erreurs doivent avoir le même message pour éviter l'énumération
        var resultUnknown  = _sut.Login("unknownuser", "password");
        var resultWrongPwd = _sut.Login("alice",       "wrongpassword");

        resultUnknown.ErrorMessage.Should().Be(resultWrongPwd.ErrorMessage,
            because: "le même message d'erreur doit être retourné pour un compte inexistant " +
                     "et un mauvais mot de passe (anti-énumération)");
    }

    #endregion

    #region ValidateToken

    [Fact]
    public void ValidateToken_ReturnsValid_ForFreshToken()
    {
        var loginResult = _sut.Login("alice", "AliceSecret456!");

        var result = _sut.ValidateToken(loginResult.Token!);

        result.Valid.Should().BeTrue();
        result.Account.Should().NotBeNull();
        result.Account!.Username.Should().Be("alice");
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForMalformedToken()
    {
        var result = _sut.ValidateToken("not.a.valid.token");

        result.Valid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForEmptyToken()
    {
        var result = _sut.ValidateToken("");

        result.Valid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForTamperedToken()
    {
        var loginResult = _sut.Login("alice", "AliceSecret456!");
        var tamperedToken = loginResult.Token! + "tampered";

        var result = _sut.ValidateToken(tamperedToken);

        result.Valid.Should().BeFalse(
            because: "un token altéré doit être rejeté");
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_AfterPasswordChange()
    {
        // Login → token → change le mot de passe → token invalide
        var loginResult = _sut.Login("alice", "AliceSecret456!");
        _accountService.ResetPassword("alice", "NewPassword789!");

        var result = _sut.ValidateToken(loginResult.Token!);

        result.Valid.Should().BeFalse(
            because: "changer le mot de passe doit invalider tous les tokens existants");
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForRevokedAccount()
    {
        var loginResult = _sut.Login("alice", "AliceSecret456!");
        _accountService.Revoke("alice");

        var result = _sut.ValidateToken(loginResult.Token!);

        result.Valid.Should().BeFalse(
            because: "révoquer un compte doit invalider ses tokens");
    }

    #endregion

    #region Logout

    [Fact]
    public void Logout_DoesNotThrow_WhenNoSession()
    {
        var act = () => _sut.Logout();

        act.Should().NotThrow(because: "logout sans session active doit être silencieux");
    }

    [Fact]
    public void Logout_ClearsToken_FromSession()
    {
        // Login → session avec token
        var loginResult = _sut.Login("alice", "AliceSecret456!");
        _sessionService.SaveSession(new SessionContext(
            GameId:        null,
            PlayerId:      null,
            PlayerName:    "alice",
            Role:          Role.Admin,
            GameLevel:     null,
            AuthToken:     loginResult.Token,
            TokenExpiresAt: loginResult.ExpiresAt,
            CreatedAt:     DateTimeOffset.UtcNow,
            UpdatedAt:     DateTimeOffset.UtcNow));

        // Logout
        _sut.Logout();

        // Vérifier que le token a été effacé
        var session = _sessionService.LoadSession();
        session.Should().NotBeNull(because: "la session ne doit pas être supprimée, seulement le token");
        session!.AuthToken.Should().BeNull(because: "logout doit effacer le token de la session");
    }

    #endregion

    #region TokenLifetime

    [Fact]
    public void TokenLifetime_IsMaxValue_ByDefault()
    {
        _sut.TokenLifetime.Should().Be(TimeSpan.MaxValue,
            because: "par défaut, les tokens n'expirent pas automatiquement (logout explicite)");
    }

    #endregion
}
