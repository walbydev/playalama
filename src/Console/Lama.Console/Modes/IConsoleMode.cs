namespace Lama.Console.Modes;

/// <summary>
/// Représente un mode d'exécution de l'application console.
/// Deux modes sont supportés : commande par commande et interactif textuel.
/// </summary>
public interface IConsoleMode
{
    /// <summary>
    /// Exécute le mode et retourne le code de sortie du processus.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le code de sortie (0 = succès, voir ExitCodes pour les autres valeurs).</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
