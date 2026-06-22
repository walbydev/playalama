namespace Lama.Console.Services;

/// <summary>
/// Dispatche une commande vers son handler en fonction du CommandContext.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatche la commande décrite par le contexte et retourne le code de sortie.
    /// </summary>
    /// <param name="context">Le contexte de la commande à exécuter.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le code de sortie (0 = succès).</returns>
    Task<int> DispatchAsync(CommandContext context, CancellationToken cancellationToken = default);
}
