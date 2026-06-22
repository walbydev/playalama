namespace Lama.Console.Modes;

/// <summary>
/// Résout le mode d'exécution de l'application en fonction des arguments de la ligne de commande.
/// </summary>
public interface IApplicationModeResolver
{
    /// <summary>
    /// Retourne le mode d'exécution approprié selon les arguments fournis.
    /// Si aucun argument n'est passé, ou si l'argument est "interactive", "shell" ou "ui",
    /// retourne le mode interactif. Sinon, retourne le mode commande par commande.
    /// </summary>
    /// <param name="args">Les arguments de la ligne de commande.</param>
    /// <returns>Le mode d'exécution résolu.</returns>
    IConsoleMode Resolve(string[] args);
}
