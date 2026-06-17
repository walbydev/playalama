namespace Lama.Contracts;

/// <summary>
/// Service de contrôle d'accès aux commandes CLI.
/// Vérifie si un rôle donné est autorisé à exécuter une commande
/// dans le contexte d'un niveau de partie.
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// Détermine si une commande est autorisée pour un rôle et un niveau de partie donnés.
    /// </summary>
    /// <param name="command">
    /// Identifiant de la commande sous la forme "groupe.action", ex: "show.hints", "system.restart".
    /// </param>
    /// <param name="role">Rôle de l'utilisateur.</param>
    /// <param name="gameLevel">Niveau de la partie en cours, ou null hors contexte de partie.</param>
    /// <returns>
    /// <see cref="AccessResult.Allowed"/> si la commande est autorisée,
    /// <see cref="AccessResult.Denied"/> avec un message explicatif sinon.
    /// </returns>
    AccessResult CheckAccess(string command, Role role, GameLevel? gameLevel = null);

    /// <summary>
    /// Retourne toutes les commandes accessibles pour un rôle et un niveau de partie donnés.
    /// </summary>
    IReadOnlySet<string> GetAllowedCommands(Role role, GameLevel? gameLevel = null);
}

/// <summary>
/// Résultat d'une vérification d'accès.
/// </summary>
/// <param name="IsAllowed">True si la commande est autorisée.</param>
/// <param name="Reason">Message explicatif en cas de refus.</param>
public record AccessResult(bool IsAllowed, string? Reason = null)
{
    /// <summary>Accès accordé.</summary>
    public static AccessResult Allowed { get; } = new(true);

    /// <summary>Accès refusé avec un motif.</summary>
    public static AccessResult Denied(string reason) => new(false, reason);
}
