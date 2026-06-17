namespace Lama.Contracts;

/// <summary>
/// Service de gestion des comptes utilisateurs persistés.
/// Gère le cycle de vie des comptes SuperAdmin et Admin dans <c>accounts.json</c>.
///
/// Seul le SuperAdmin peut créer, modifier et révoquer des comptes Admin.
/// Il ne peut exister qu'un seul compte SuperAdmin.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Indique si le système a été initialisé (compte SuperAdmin existant).
    /// Retourne false si <c>accounts.json</c> est absent ou vide.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Crée le compte SuperAdmin lors de la première initialisation.
    /// Lève une exception si un SuperAdmin existe déjà.
    /// </summary>
    /// <param name="username">Nom du SuperAdmin.</param>
    /// <param name="password">Mot de passe en clair (sera hashé en PBKDF2).</param>
    /// <returns>Le compte SuperAdmin créé.</returns>
    Account CreateSuperAdmin(string username, string password);

    /// <summary>
    /// Crée un compte Admin. Réservé au SuperAdmin.
    /// </summary>
    /// <param name="username">Nom du nouvel admin (unique).</param>
    /// <param name="password">Mot de passe en clair.</param>
    /// <returns>Le compte Admin créé.</returns>
    Account CreateAdmin(string username, string password);

    /// <summary>
    /// Retourne tous les comptes (actifs et révoqués).
    /// </summary>
    IReadOnlyList<Account> GetAll();

    /// <summary>
    /// Retourne un compte par son nom d'utilisateur (insensible à la casse).
    /// Retourne null si introuvable.
    /// </summary>
    Account? FindByUsername(string username);

    /// <summary>
    /// Révoque un compte Admin (passe <c>Active = false</c>).
    /// Impossible de révoquer le SuperAdmin.
    /// </summary>
    /// <param name="username">Nom de l'admin à révoquer.</param>
    /// <returns>True si révoqué, false si introuvable.</returns>
    bool Revoke(string username);

    /// <summary>
    /// Réinitialise le mot de passe d'un compte.
    /// </summary>
    /// <param name="username">Nom de l'utilisateur.</param>
    /// <param name="newPassword">Nouveau mot de passe en clair.</param>
    /// <returns>True si mis à jour, false si introuvable.</returns>
    bool ResetPassword(string username, string newPassword);

    /// <summary>
    /// Vérifie qu'un mot de passe correspond au hash stocké pour un compte.
    /// </summary>
    /// <param name="account">Le compte à vérifier.</param>
    /// <param name="password">Le mot de passe en clair à tester.</param>
    bool VerifyPassword(Account account, string password);
}
