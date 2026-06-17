using FluentAssertions;
using Lama.Contracts;
using Lama.Infrastructure.Session;
using Lama.Infrastructure.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Infrastructure.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="SessionService"/>.
/// Vérifie la persistance et le chargement du contexte de session.
/// </summary>
[Collection(Helpers.SequentialTestCollection.Name)]
public class SessionServiceTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        _tempDir = new TempDirectory();
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir.Path);
        _sut = new SessionService(NullLogger<SessionService>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        _tempDir.Dispose();
    }

    // Helper : crée une session de test valide
    private static SessionContext CreateTestSession(
        string? gameId   = "game123",
        string? playerId = "player456",
        Role role        = Role.Player,
        GameLevel? level = GameLevel.Standard) => new(
            GameId:         gameId,
            PlayerId:       playerId,
            PlayerName:     "TestPlayer",
            Role:           role,
            GameLevel:      level,
            AuthToken:      null,
            TokenExpiresAt: null,
            CreatedAt:      DateTimeOffset.UtcNow,
            UpdatedAt:      DateTimeOffset.UtcNow);

    #region SessionFilePath

    [Fact]
    public void SessionFilePath_PointsToCorrectLocation()
    {
        _sut.SessionFilePath.Should().EndWith("session.json",
            because: "le fichier de session doit s'appeler session.json");
        _sut.SessionFilePath.Should().StartWith(_tempDir.Path,
            because: "le chemin doit respecter LAMA_SESSION_DIR");
    }

    #endregion

    #region LoadSession

    [Fact]
    public void LoadSession_ReturnsNull_WhenNoFile()
    {
        var session = _sut.LoadSession();

        session.Should().BeNull(
            because: "LoadSession doit retourner null si aucun fichier n'existe");
    }

    [Fact]
    public void LoadSession_ReturnsNull_WhenFileIsCorrupted()
    {
        File.WriteAllText(_sut.SessionFilePath, "{ invalid json !!!");

        var session = _sut.LoadSession();

        session.Should().BeNull(
            because: "un fichier corrompu doit être toléré (null retourné, pas d'exception)");
    }

    [Fact]
    public void LoadSession_ReturnsNull_WhenFileIsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_sut.SessionFilePath)!);
        File.WriteAllText(_sut.SessionFilePath, "");

        var session = _sut.LoadSession();

        session.Should().BeNull();
    }

    #endregion

    #region SaveSession + LoadSession (aller-retour)

    [Fact]
    public void SaveAndLoad_PreservesAllFields()
    {
        var original = CreateTestSession();

        _sut.SaveSession(original);
        var loaded = _sut.LoadSession();

        loaded.Should().NotBeNull();
        loaded!.GameId.Should().Be(original.GameId);
        loaded.PlayerId.Should().Be(original.PlayerId);
        loaded.PlayerName.Should().Be(original.PlayerName);
        loaded.Role.Should().Be(original.Role);
        loaded.GameLevel.Should().Be(original.GameLevel);
        loaded.AuthToken.Should().Be(original.AuthToken);
        loaded.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromSeconds(1));
        loaded.UpdatedAt.Should().BeCloseTo(original.UpdatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SaveSession_CreatesFileOnDisk()
    {
        _sut.SaveSession(CreateTestSession());

        File.Exists(_sut.SessionFilePath).Should().BeTrue(
            because: "SaveSession doit créer session.json");
    }

    [Fact]
    public void SaveSession_CreatesDirectoryIfMissing()
    {
        // Le répertoire n'existe pas encore
        var nestedDir = Path.Combine(_tempDir.Path, "nested", "subdir");
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", nestedDir);
        var sut2 = new SessionService(NullLogger<SessionService>.Instance);

        sut2.SaveSession(CreateTestSession());

        File.Exists(sut2.SessionFilePath).Should().BeTrue(
            because: "SaveSession doit créer les répertoires manquants");
    }

    [Fact]
    public void SaveSession_Overwrites_ExistingSession()
    {
        _sut.SaveSession(CreateTestSession(gameId: "game1"));
        _sut.SaveSession(CreateTestSession(gameId: "game2"));

        var loaded = _sut.LoadSession();

        loaded!.GameId.Should().Be("game2",
            because: "la deuxième sauvegarde doit écraser la première");
    }

    [Theory]
    [InlineData(Role.SuperAdmin)]
    [InlineData(Role.Admin)]
    [InlineData(Role.Host)]
    [InlineData(Role.Player)]
    [InlineData(Role.Spectator)]
    public void SaveAndLoad_PreservesAllRoles(Role role)
    {
        _sut.SaveSession(CreateTestSession(role: role));
        var loaded = _sut.LoadSession();

        loaded!.Role.Should().Be(role,
            because: $"le rôle {role} doit être correctement sérialisé/désérialisé");
    }

    [Theory]
    [InlineData(GameLevel.Casual)]
    [InlineData(GameLevel.Standard)]
    [InlineData(GameLevel.Competitive)]
    [InlineData(GameLevel.Tournament)]
    public void SaveAndLoad_PreservesAllGameLevels(GameLevel level)
    {
        _sut.SaveSession(CreateTestSession(level: level));
        var loaded = _sut.LoadSession();

        loaded!.GameLevel.Should().Be(level);
    }

    [Fact]
    public void SaveAndLoad_HandlesNullFields()
    {
        var session = CreateTestSession(gameId: null, playerId: null, level: null);

        _sut.SaveSession(session);
        var loaded = _sut.LoadSession();

        loaded!.GameId.Should().BeNull();
        loaded.PlayerId.Should().BeNull();
        loaded.GameLevel.Should().BeNull();
    }

    [Fact]
    public void SaveAndLoad_PreservesAuthToken()
    {
        const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test";
        var session = CreateTestSession() with
        {
            AuthToken      = token,
            TokenExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
        };

        _sut.SaveSession(session);
        var loaded = _sut.LoadSession();

        loaded!.AuthToken.Should().Be(token);
        loaded.TokenExpiresAt.Should().NotBeNull();
    }

    #endregion

    #region ClearSession

    [Fact]
    public void ClearSession_DeletesFile_WhenExists()
    {
        _sut.SaveSession(CreateTestSession());

        _sut.ClearSession();

        File.Exists(_sut.SessionFilePath).Should().BeFalse(
            because: "ClearSession doit supprimer session.json");
    }

    [Fact]
    public void ClearSession_DoesNotThrow_WhenFileDoesNotExist()
    {
        var act = () => _sut.ClearSession();

        act.Should().NotThrow(
            because: "ClearSession doit être idempotent même sans fichier");
    }

    [Fact]
    public void LoadSession_ReturnsNull_AfterClear()
    {
        _sut.SaveSession(CreateTestSession());
        _sut.ClearSession();

        var session = _sut.LoadSession();

        session.Should().BeNull();
    }

    #endregion

    #region Isolation LAMA_SESSION_DIR

    [Fact]
    public void TwoInstances_WithDifferentDirs_AreIsolated()
    {
        using var dir2 = new TempDirectory();

        _sut.SaveSession(CreateTestSession(gameId: "game-instance-1"));

        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", dir2.Path);
        var sut2 = new SessionService(NullLogger<SessionService>.Instance);

        var session2 = sut2.LoadSession();

        session2.Should().BeNull(
            because: "deux instances avec des répertoires différents ne doivent pas partager la session");
    }

    #endregion
}
