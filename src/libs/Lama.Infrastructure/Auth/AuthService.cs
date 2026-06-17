using System.Security.Cryptography;
using System.Text;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Auth;

/// <summary>
/// Implémentation de <see cref="IAuthService"/> basée sur des tokens HMAC-SHA256.
///
/// Format du token (avant encodage Base64) :
/// <code>
///   {accountId}|{expiresAtUnixSeconds}|{hmac}
/// </code>
/// Le HMAC est calculé sur <c>{accountId}|{expiresAtUnixSeconds}</c>
/// avec une clé dérivée du hash du mot de passe du compte.
///
/// Propriétés :
/// <list type="bullet">
///   <item>Pas de dépendance externe (BCL .NET uniquement)</item>
///   <item>Le token est lié au mot de passe — révoqué automatiquement si le mdp change</item>
///   <item>Durée configurable — par défaut : pas d'expiration (logout explicite)</item>
/// </list>
/// </summary>
public sealed class AuthService : IAuthService
{
    // Durée infinie par défaut — le logout explicite est requis.
    // Peut être surchargée via IConfiguration si besoin.
    private static readonly TimeSpan DefaultLifetime = TimeSpan.MaxValue;

    private readonly IAccountService _accountService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AuthService> _logger;

    /// <inheritdoc />
    public TimeSpan TokenLifetime => DefaultLifetime;

    /// <summary>Initialise le service d'authentification.</summary>
    public AuthService(
        IAccountService accountService,
        ISessionService sessionService,
        ILogger<AuthService> logger)
    {
        _accountService = accountService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public LoginResult Login(string username, string password)
    {
        var account = _accountService.FindByUsername(username);

        if (account is null || !account.Active)
        {
            _logger.LogWarning("Tentative de login échouée : compte inconnu ou inactif ({Username})", username);
            return LoginResult.Fail("Nom d'utilisateur ou mot de passe incorrect.");
        }

        if (!_accountService.VerifyPassword(account, password))
        {
            _logger.LogWarning("Tentative de login échouée : mauvais mot de passe ({Username})", username);
            return LoginResult.Fail("Nom d'utilisateur ou mot de passe incorrect.");
        }

        var expiresAt = TokenLifetime == TimeSpan.MaxValue
            ? DateTimeOffset.MaxValue
            : DateTimeOffset.UtcNow.Add(TokenLifetime);

        var token = GenerateToken(account, expiresAt);

        _logger.LogInformation("Login réussi : {Username} ({Role})", account.Username, account.Role);
        return LoginResult.Ok(account, token, expiresAt);
    }

    /// <inheritdoc />
    public void Logout()
    {
        var session = _sessionService.LoadSession();
        if (session is null) return;

        // Effacer le token de la session en créant une session sans token
        var updated = session with
        {
            AuthToken    = null,
            TokenExpiresAt = null,
            UpdatedAt    = DateTimeOffset.UtcNow
        };
        _sessionService.SaveSession(updated);
        _logger.LogInformation("Logout effectué pour {PlayerName}", session.PlayerName ?? "(inconnu)");
    }

    /// <inheritdoc />
    public TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts   = decoded.Split('|');

            if (parts.Length != 3)
                return TokenValidationResult.Fail("Format de token invalide.");

            var accountId      = parts[0];
            var expiresSeconds = long.Parse(parts[1]);
            var providedHmac   = parts[2];

            // Vérifier l'expiration
            if (expiresSeconds != long.MaxValue)
            {
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds);
                if (DateTimeOffset.UtcNow > expiresAt)
                    return TokenValidationResult.Fail("Token expiré. Reconnectez-vous avec 'lama login'.");
            }

            // Retrouver le compte
            var account = _accountService.GetAll()
                .FirstOrDefault(a => a.Id == accountId);

            if (account is null || !account.Active)
                return TokenValidationResult.Fail("Compte introuvable ou révoqué.");

            // Vérifier la signature HMAC
            var expectedHmac = ComputeHmac(accountId, expiresSeconds, account.PasswordHash);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(providedHmac),
                    Convert.FromBase64String(expectedHmac)))
            {
                _logger.LogWarning("Token invalide (HMAC incorrect) pour accountId={AccountId}", accountId);
                return TokenValidationResult.Fail("Token invalide ou altéré.");
            }

            return TokenValidationResult.Ok(account);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            _logger.LogWarning(ex, "Token malformé");
            return TokenValidationResult.Fail("Token malformé.");
        }
    }

    // ─── Helpers privés ──────────────────────────────────────────────────────

    private static string GenerateToken(Account account, DateTimeOffset expiresAt)
    {
        var expiresSeconds = expiresAt == DateTimeOffset.MaxValue
            ? long.MaxValue
            : expiresAt.ToUnixTimeSeconds();

        var hmac    = ComputeHmac(account.Id, expiresSeconds, account.PasswordHash);
        var payload = $"{account.Id}|{expiresSeconds}|{hmac}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Calcule le HMAC-SHA256 du payload en utilisant le hash du mot de passe comme clé.
    /// Ainsi, changer le mot de passe invalide automatiquement tous les tokens existants.
    /// </summary>
    private static string ComputeHmac(string accountId, long expiresSeconds, string passwordHashAsKey)
    {
        var key     = Convert.FromBase64String(passwordHashAsKey);
        var message = Encoding.UTF8.GetBytes($"{accountId}|{expiresSeconds}");
        var hmac    = HMACSHA256.HashData(key, message);
        return Convert.ToBase64String(hmac);
    }
}
