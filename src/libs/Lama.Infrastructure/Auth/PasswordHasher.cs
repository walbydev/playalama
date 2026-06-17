using System.Security.Cryptography;

namespace Lama.Infrastructure.Auth;

/// <summary>
/// Utilitaire de hachage de mots de passe basé sur PBKDF2-HMAC-SHA256.
///
/// Paramètres choisis :
/// <list type="bullet">
///   <item>Algorithme : HMAC-SHA256</item>
///   <item>Itérations : 310 000 (recommandation OWASP 2023 pour PBKDF2-SHA256)</item>
///   <item>Taille du sel : 32 octets (256 bits)</item>
///   <item>Taille du hash : 32 octets (256 bits)</item>
///   <item>Encodage : Base64 standard</item>
/// </list>
///
/// Tout est dans la BCL .NET — aucune dépendance externe.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations  = 310_000;
    private const int SaltSize    = 32; // octets
    private const int HashSize    = 32; // octets

    /// <summary>
    /// Génère un sel aléatoire cryptographiquement sûr.
    /// </summary>
    /// <returns>Le sel en Base64.</returns>
    public static string GenerateSalt()
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return Convert.ToBase64String(salt);
    }

    /// <summary>
    /// Calcule le hash PBKDF2 d'un mot de passe avec le sel fourni.
    /// </summary>
    /// <param name="password">Mot de passe en clair.</param>
    /// <param name="saltBase64">Sel en Base64 (généré par <see cref="GenerateSalt"/>).</param>
    /// <returns>Le hash en Base64.</returns>
    public static string Hash(string password, string saltBase64)
    {
        var salt  = Convert.FromBase64String(saltBase64);
        var hash  = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Vérifie qu'un mot de passe correspond à un hash stocké.
    /// Utilise une comparaison en temps constant pour éviter les timing attacks.
    /// </summary>
    /// <param name="password">Mot de passe en clair à vérifier.</param>
    /// <param name="saltBase64">Sel stocké (Base64).</param>
    /// <param name="hashBase64">Hash stocké (Base64).</param>
    /// <returns>True si le mot de passe correspond.</returns>
    public static bool Verify(string password, string saltBase64, string hashBase64)
    {
        var computedHash = Hash(password, saltBase64);
        // CryptographicOperations.FixedTimeEquals — comparaison en temps constant
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computedHash),
            Convert.FromBase64String(hashBase64));
    }
}
