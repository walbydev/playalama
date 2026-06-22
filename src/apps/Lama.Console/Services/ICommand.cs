namespace Lama.Console.Services;

/// <summary>
/// Représente une commande CLI exécutable.
/// Chaque commande correspond à une action précise (ex: game.create, play.move).
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Identifiant de la commande au format "groupe.action" (ex: "play.move").
    /// </summary>
    string CommandId { get; }

    /// <summary>
    /// Exécute la commande avec le contexte fourni.
    /// </summary>
    /// <param name="context">Le contexte contenant les arguments et options parsés.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le code de sortie (0 = succès).</returns>
    Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default);
}
