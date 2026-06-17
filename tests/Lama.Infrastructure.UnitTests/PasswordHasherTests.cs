using FluentAssertions;
using Lama.Infrastructure.Auth;

namespace Lama.Infrastructure.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="PasswordHasher"/>.
/// Vérifie le hachage PBKDF2-HMAC-SHA256 et la résistance aux attaques.
/// </summary>
public class PasswordHasherTests
{
    #region GenerateSalt

    [Fact]
    public void GenerateSalt_ReturnsNonEmptyBase64String()
    {
        var salt = PasswordHasher.GenerateSalt();

        salt.Should().NotBeNullOrEmpty();
        // Vérifier que c'est du Base64 valide
        var act = () => Convert.FromBase64String(salt);
        act.Should().NotThrow(because: "GenerateSalt doit retourner du Base64 valide");
    }

    [Fact]
    public void GenerateSalt_ReturnsDifferentValuesEachCall()
    {
        var salt1 = PasswordHasher.GenerateSalt();
        var salt2 = PasswordHasher.GenerateSalt();
        var salt3 = PasswordHasher.GenerateSalt();

        salt1.Should().NotBe(salt2, because: "chaque sel doit être unique");
        salt2.Should().NotBe(salt3, because: "chaque sel doit être unique");
        salt1.Should().NotBe(salt3, because: "chaque sel doit être unique");
    }

    [Fact]
    public void GenerateSalt_HasExpectedByteLength()
    {
        var salt  = PasswordHasher.GenerateSalt();
        var bytes = Convert.FromBase64String(salt);

        bytes.Should().HaveCount(32, because: "le sel doit faire 32 octets (256 bits)");
    }

    #endregion

    #region Hash

    [Fact]
    public void Hash_ReturnsDeterministicResult_SameSalt()
    {
        const string password = "MotDePasseTest123!";
        var salt = PasswordHasher.GenerateSalt();

        var hash1 = PasswordHasher.Hash(password, salt);
        var hash2 = PasswordHasher.Hash(password, salt);

        hash1.Should().Be(hash2,
            because: "le même mot de passe avec le même sel doit toujours donner le même hash");
    }

    [Fact]
    public void Hash_ReturnsDifferentResults_DifferentSalts()
    {
        const string password = "MotDePasseTest123!";
        var salt1 = PasswordHasher.GenerateSalt();
        var salt2 = PasswordHasher.GenerateSalt();

        var hash1 = PasswordHasher.Hash(password, salt1);
        var hash2 = PasswordHasher.Hash(password, salt2);

        hash1.Should().NotBe(hash2,
            because: "des sels différents doivent produire des hashs différents");
    }

    [Fact]
    public void Hash_ReturnsDifferentResults_DifferentPasswords_SameSalt()
    {
        var salt = PasswordHasher.GenerateSalt();

        var hash1 = PasswordHasher.Hash("password1", salt);
        var hash2 = PasswordHasher.Hash("password2", salt);

        hash1.Should().NotBe(hash2,
            because: "des mots de passe différents doivent produire des hashs différents");
    }

    [Fact]
    public void Hash_ReturnsValidBase64()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("test", salt);

        var act = () => Convert.FromBase64String(hash);
        act.Should().NotThrow(because: "Hash doit retourner du Base64 valide");
    }

    [Fact]
    public void Hash_HasExpectedByteLength()
    {
        var salt  = PasswordHasher.GenerateSalt();
        var hash  = PasswordHasher.Hash("test", salt);
        var bytes = Convert.FromBase64String(hash);

        bytes.Should().HaveCount(32, because: "le hash doit faire 32 octets (256 bits)");
    }

    #endregion

    #region Verify

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        const string password = "MonMotDePasse!42";
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(password, salt);

        var result = PasswordHasher.Verify(password, salt, hash);

        result.Should().BeTrue(because: "le mot de passe correct doit être vérifié avec succès");
    }

    [Fact]
    public void Verify_ReturnsFalse_ForIncorrectPassword()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("correct", salt);

        var result = PasswordHasher.Verify("incorrect", salt, hash);

        result.Should().BeFalse(because: "un mauvais mot de passe ne doit pas être accepté");
    }

    [Fact]
    public void Verify_ReturnsFalse_ForEmptyPassword_WhenHashedNonEmpty()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("motdepasse", salt);

        var result = PasswordHasher.Verify("", salt, hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForSimilarButDifferentPassword()
    {
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash("Password123", salt);

        // Variantes proches — toutes doivent échouer
        PasswordHasher.Verify("password123",  salt, hash).Should().BeFalse(because: "casse différente");
        PasswordHasher.Verify("Password1234", salt, hash).Should().BeFalse(because: "caractère supplémentaire");
        PasswordHasher.Verify("Password12",   salt, hash).Should().BeFalse(because: "caractère manquant");
        PasswordHasher.Verify(" Password123", salt, hash).Should().BeFalse(because: "espace en tête");
    }

    [Fact]
    public void Verify_ReturnsFalse_WithWrongSalt()
    {
        var salt1 = PasswordHasher.GenerateSalt();
        var salt2 = PasswordHasher.GenerateSalt();
        var hash  = PasswordHasher.Hash("motdepasse", salt1);

        var result = PasswordHasher.Verify("motdepasse", salt2, hash);

        result.Should().BeFalse(because: "un sel différent doit invalider la vérification");
    }

    [Fact]
    public void Verify_AcceptsUnicodePasswords()
    {
        const string password = "Ünîcödé@Pässwörd!éàü";
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(password, salt);

        var result = PasswordHasher.Verify(password, salt, hash);

        result.Should().BeTrue(because: "les mots de passe Unicode doivent être supportés");
    }

    [Fact]
    public void Verify_AcceptsVeryLongPasswords()
    {
        var password = new string('a', 1000);
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(password, salt);

        var result = PasswordHasher.Verify(password, salt, hash);

        result.Should().BeTrue(because: "les mots de passe très longs doivent être supportés");
    }

    #endregion
}
