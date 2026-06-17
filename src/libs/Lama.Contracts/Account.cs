namespace Lama.Contracts;

/// <summary>
/// Représente un compte utilisateur persisté dans <c>accounts.json</c>.
/// Seuls les rôles <see cref="Role.SuperAdmin"/> et <see cref="Role.Admin"/>
/// ont des comptes persistés — Host, Player et Spectator sont des rôles
/// de session attribués dynamiquement lors d'une partie.
/// </summary>
/// <param name="Id">Identifiant unique du compte (GUID sans tirets).</param>
/// <param name="Username">Nom d'utilisateur (unique, insensible à la casse).</param>
/// <param name="Role">Rôle du compte : SuperAdmin ou Admin uniquement.</param>
/// <param name="PasswordHash">Hash PBKDF2 du mot de passe (base64).</param>
/// <param name="Salt">Sel aléatoire utilisé pour le hash (base64).</param>
/// <param name="CreatedAt">Date de création du compte (UTC).</param>
/// <param name="Active">Indique si le compte est actif. Un compte révoqué reste dans le fichier.</param>
public record Account(
    string Id,
    string Username,
    Role Role,
    string PasswordHash,
    string Salt,
    DateTimeOffset CreatedAt,
    bool Active = true);

/// <summary>
/// Résultat d'une opération de login.
/// </summary>
/// <param name="Success">Indique si l'authentification a réussi.</param>
/// <param name="Account">Le compte authentifié, ou null si échec.</param>
/// <param name="Token">Le token de session signé, ou null si échec.</param>
/// <param name="ExpiresAt">Date d'expiration du token (UTC).</param>
/// <param name="ErrorMessage">Message d'erreur en cas d'échec.</param>
public record LoginResult(
    bool Success,
    Account? Account = null,
    string? Token = null,
    DateTimeOffset? ExpiresAt = null,
    string? ErrorMessage = null)
{
    /// <summary>Crée un résultat de succès.</summary>
    public static LoginResult Ok(Account account, string token, DateTimeOffset expiresAt) =>
        new(true, account, token, expiresAt);

    /// <summary>Crée un résultat d'échec.</summary>
    public static LoginResult Fail(string reason) =>
        new(false, ErrorMessage: reason);
}

/// <summary>
/// Résultat d'une vérification de token.
/// </summary>
/// <param name="Valid">Indique si le token est valide et non expiré.</param>
/// <param name="Account">Le compte associé au token, ou null si invalide.</param>
/// <param name="ErrorMessage">Raison de l'invalidité, ou null si valide.</param>
public record TokenValidationResult(
    bool Valid,
    Account? Account = null,
    string? ErrorMessage = null)
{
    /// <summary>Crée un résultat valide.</summary>
    public static TokenValidationResult Ok(Account account) =>
        new(true, account);

    /// <summary>Crée un résultat invalide.</summary>
    public static TokenValidationResult Fail(string reason) =>
        new(false, ErrorMessage: reason);
}
