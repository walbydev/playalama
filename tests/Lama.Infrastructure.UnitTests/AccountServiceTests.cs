using FluentAssertions;
using Lama.Contracts;
using Lama.Infrastructure.Auth;
using Lama.Infrastructure.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Infrastructure.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="AccountService"/>.
/// Vérifie le CRUD des comptes et la gestion des mots de passe.
/// Chaque test utilise un répertoire temporaire isolé via LAMA_SESSION_DIR.
/// </summary>
[Collection(Helpers.SequentialTestCollection.Name)]
public class AccountServiceTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _tempDir = new TempDirectory();
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir.Path);
        _sut = new AccountService(NullLogger<AccountService>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        _tempDir.Dispose();
    }

    #region IsInitialized

    [Fact]
    public void IsInitialized_ReturnsFalse_WhenNoAccountsFile()
    {
        _sut.IsInitialized.Should().BeFalse(
            because: "le système ne doit pas être initialisé si accounts.json n'existe pas");
    }

    [Fact]
    public void IsInitialized_ReturnsTrue_AfterSuperAdminCreated()
    {
        _sut.CreateSuperAdmin("superadmin", "SuperSecret123!");

        _sut.IsInitialized.Should().BeTrue(
            because: "le système doit être initialisé après la création du SuperAdmin");
    }

    #endregion

    #region CreateSuperAdmin

    [Fact]
    public void CreateSuperAdmin_ReturnsAccount_WithSuperAdminRole()
    {
        var account = _sut.CreateSuperAdmin("superadmin", "SuperSecret123!");

        account.Should().NotBeNull();
        account.Username.Should().Be("superadmin");
        account.Role.Should().Be(Role.SuperAdmin);
        account.Active.Should().BeTrue();
        account.Id.Should().NotBeNullOrEmpty();
        account.PasswordHash.Should().NotBeNullOrEmpty();
        account.Salt.Should().NotBeNullOrEmpty();
        account.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateSuperAdmin_ThrowsInvalidOperationException_WhenCalledTwice()
    {
        _sut.CreateSuperAdmin("superadmin", "SuperSecret123!");

        var act = () => _sut.CreateSuperAdmin("superadmin2", "AnotherSecret456!");

        act.Should().Throw<InvalidOperationException>(
            because: "un seul SuperAdmin peut exister");
    }

    [Fact]
    public void CreateSuperAdmin_PersistsToFile()
    {
        _sut.CreateSuperAdmin("superadmin", "SuperSecret123!");

        var accountsFile = Path.Combine(_tempDir.Path, "accounts.json");
        File.Exists(accountsFile).Should().BeTrue(
            because: "accounts.json doit être créé lors de la création du SuperAdmin");
    }

    [Fact]
    public void CreateSuperAdmin_DoesNotStorePasswordInClearText()
    {
        const string password = "SuperSecret123!";
        _sut.CreateSuperAdmin("superadmin", password);

        var accountsFile = Path.Combine(_tempDir.Path, "accounts.json");
        var content = File.ReadAllText(accountsFile);

        content.Should().NotContain(password,
            because: "le mot de passe en clair ne doit jamais être stocké");
    }

    #endregion

    #region CreateAdmin

    [Fact]
    public void CreateAdmin_ReturnsAccount_WithAdminRole()
    {
        var account = _sut.CreateAdmin("adminuser", "AdminSecret789!");

        account.Username.Should().Be("adminuser");
        account.Role.Should().Be(Role.Admin);
        account.Active.Should().BeTrue();
    }

    [Fact]
    public void CreateAdmin_ThrowsException_WhenUsernameAlreadyExists()
    {
        _sut.CreateAdmin("alice", "Secret1!");

        var act = () => _sut.CreateAdmin("alice", "Secret2!");

        act.Should().Throw<InvalidOperationException>(
            because: "deux comptes ne peuvent pas avoir le même nom d'utilisateur");
    }

    [Fact]
    public void CreateAdmin_IsCaseInsensitiveForUsername()
    {
        _sut.CreateAdmin("Alice", "Secret1!");

        var act = () => _sut.CreateAdmin("alice", "Secret2!");

        act.Should().Throw<InvalidOperationException>(
            because: "les noms d'utilisateur sont insensibles à la casse");
    }

    [Fact]
    public void CreateAdmin_MultipleAdmins_AreAllPersisted()
    {
        _sut.CreateAdmin("admin1", "Secret1!");
        _sut.CreateAdmin("admin2", "Secret2!");
        _sut.CreateAdmin("admin3", "Secret3!");

        var all = _sut.GetAll();

        all.Should().HaveCount(3,
            because: "les 3 admins doivent être persistés");
        all.Should().Contain(a => a.Username == "admin1");
        all.Should().Contain(a => a.Username == "admin2");
        all.Should().Contain(a => a.Username == "admin3");
    }

    #endregion

    #region FindByUsername

    [Fact]
    public void FindByUsername_ReturnsAccount_WhenExists()
    {
        _sut.CreateAdmin("alice", "Secret123!");

        var found = _sut.FindByUsername("alice");

        found.Should().NotBeNull();
        found!.Username.Should().Be("alice");
    }

    [Fact]
    public void FindByUsername_IsCaseInsensitive()
    {
        _sut.CreateAdmin("Alice", "Secret123!");

        var found = _sut.FindByUsername("ALICE");

        found.Should().NotBeNull(because: "FindByUsername doit être insensible à la casse");
    }

    [Fact]
    public void FindByUsername_ReturnsNull_WhenNotExists()
    {
        var found = _sut.FindByUsername("inexistant");

        found.Should().BeNull();
    }

    #endregion

    #region Revoke

    [Fact]
    public void Revoke_SetsActiveToFalse_ForExistingAdmin()
    {
        _sut.CreateAdmin("alice", "Secret123!");

        var revoked = _sut.Revoke("alice");

        revoked.Should().BeTrue();
        var account = _sut.FindByUsername("alice");
        account!.Active.Should().BeFalse(
            because: "un compte révoqué doit être inactif");
    }

    [Fact]
    public void Revoke_ReturnsFalse_WhenAccountNotFound()
    {
        var result = _sut.Revoke("inexistant");

        result.Should().BeFalse();
    }

    [Fact]
    public void Revoke_ThrowsException_WhenRevokingSuperAdmin()
    {
        _sut.CreateSuperAdmin("superadmin", "SuperSecret123!");

        var act = () => _sut.Revoke("superadmin");

        act.Should().Throw<InvalidOperationException>(
            because: "le compte SuperAdmin ne peut pas être révoqué");
    }

    [Fact]
    public void Revoke_PreservesAccountInFile_ForAudit()
    {
        _sut.CreateAdmin("alice", "Secret123!");
        _sut.Revoke("alice");

        var all = _sut.GetAll();

        all.Should().ContainSingle(because: "le compte révoqué doit rester dans le fichier pour l'audit");
        all.First().Active.Should().BeFalse();
    }

    #endregion

    #region ResetPassword

    [Fact]
    public void ResetPassword_UpdatesHash_ForExistingAccount()
    {
        _sut.CreateAdmin("alice", "OldSecret123!");
        var before = _sut.FindByUsername("alice")!.PasswordHash;

        _sut.ResetPassword("alice", "NewSecret456!");
        var after = _sut.FindByUsername("alice")!.PasswordHash;

        after.Should().NotBe(before,
            because: "le hash doit changer après une réinitialisation de mot de passe");
    }

    [Fact]
    public void ResetPassword_ReturnsFalse_WhenAccountNotFound()
    {
        var result = _sut.ResetPassword("inexistant", "newpass");

        result.Should().BeFalse();
    }

    [Fact]
    public void ResetPassword_NewPasswordVerifiesCorrectly()
    {
        _sut.CreateAdmin("alice", "OldSecret123!");
        _sut.ResetPassword("alice", "NewSecret456!");

        var account = _sut.FindByUsername("alice")!;
        var valid = _sut.VerifyPassword(account, "NewSecret456!");

        valid.Should().BeTrue(because: "le nouveau mot de passe doit être vérifiable");
    }

    [Fact]
    public void ResetPassword_OldPasswordNoLongerWorks()
    {
        _sut.CreateAdmin("alice", "OldSecret123!");
        _sut.ResetPassword("alice", "NewSecret456!");

        var account = _sut.FindByUsername("alice")!;
        var oldValid = _sut.VerifyPassword(account, "OldSecret123!");

        oldValid.Should().BeFalse(
            because: "l'ancien mot de passe ne doit plus fonctionner après réinitialisation");
    }

    #endregion

    #region VerifyPassword

    [Fact]
    public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
    {
        const string password = "MonMotDePasse42!";
        _sut.CreateAdmin("alice", password);
        var account = _sut.FindByUsername("alice")!;

        var result = _sut.VerifyPassword(account, password);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForIncorrectPassword()
    {
        _sut.CreateAdmin("alice", "CorrectPassword!");
        var account = _sut.FindByUsername("alice")!;

        var result = _sut.VerifyPassword(account, "WrongPassword!");

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_IsCaseSensitive()
    {
        _sut.CreateAdmin("alice", "Password123!");
        var account = _sut.FindByUsername("alice")!;

        _sut.VerifyPassword(account, "password123!").Should().BeFalse(
            because: "la vérification du mot de passe est sensible à la casse");
        _sut.VerifyPassword(account, "PASSWORD123!").Should().BeFalse();
        _sut.VerifyPassword(account, "Password123!").Should().BeTrue();
    }

    #endregion

    #region Persistance inter-instances

    [Fact]
    public void AccountCreatedByOneInstance_IsVisibleToAnotherInstance()
    {
        // Arrange — instance 1 crée un compte
        _sut.CreateAdmin("alice", "Secret123!");

        // Act — instance 2 dans le même répertoire
        var sut2 = new AccountService(NullLogger<AccountService>.Instance);
        var found = sut2.FindByUsername("alice");

        // Assert
        found.Should().NotBeNull(
            because: "les comptes doivent être partagés entre instances via accounts.json");
    }

    #endregion
}
