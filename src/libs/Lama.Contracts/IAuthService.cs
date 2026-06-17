namespace Lama.Contracts;

/// <summary>
/// Service d'authentification — gère le login, le logout et la validation des tokens.
///
/// Le token est un HMAC-SHA256 signé localement, sans dépendance externe.
/// Format : base64(accountId + "|" + expiresAt + "|" + hmac)
///
/// Le token est stocké dans la session (<see cref="ISessionService"/>)
/// et vérifié à chaque commande nécessitant un rôle Admin ou SuperAdmin.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authentifie un utilisateur et retourne un token de session signé.
    /// </summary>
    /// <param name="username">Nom d'utilisateur.</param>
    /// <param name="password">Mot de passe en clair.</param>
    /// <returns>
    /// <see cref="LoginResult.Success"/> = true avec le token si les credentials sont valides,
    /// false avec un message d'erreur sinon.
    /// </returns>
    LoginResult Login(string username, string password);

    /// <summary>
    /// Invalide le token courant (logout).
    /// Supprime le token de la session persistée.
    /// </summary>
    void Logout();

    /// <summary>
    /// Vérifie qu'un token est valide, non expiré et correspond à un compte actif.
    /// </summary>
    /// <param name="token">Le token à valider.</param>
    /// <returns>
    /// <see cref="TokenValidationResult.Valid"/> = true avec le compte si valide,
    /// false avec un message d'erreur sinon.
    /// </returns>
    TokenValidationResult ValidateToken(string token);

    /// <summary>
    /// Durée de validité d'un token après le login.
    /// Par défaut : pas d'expiration automatique (logout explicite requis).
    /// Configurable dans <c>config.json</c>.
    /// </summary>
    TimeSpan TokenLifetime { get; }
}
