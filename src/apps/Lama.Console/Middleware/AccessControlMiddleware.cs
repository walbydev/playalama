using Lama.Console.Services;
using Lama.Contracts;

namespace Lama.Console.Commands.Middleware;

/// <summary>
/// Middleware de contrôle d'accès.
/// S'intercale avant l'exécution de chaque commande CLI pour vérifier
/// que le rôle de l'utilisateur et le niveau de partie autorisent l'action.
///
/// En cas de refus, retourne le exit code 11 (droits insuffisants)
/// et affiche un message explicatif sans exécuter la commande.
/// </summary>
public sealed class AccessControlMiddleware
{
    /// <summary>Exit code retourné lors d'un accès refusé (droits insuffisants).</summary>
    public const int ExitCodeAccessDenied = 11;

    private readonly IAccessControlService _accessControl;

    public AccessControlMiddleware(IAccessControlService accessControl)
    {
        _accessControl = accessControl;
    }

    /// <summary>
    /// Intercepte une commande et vérifie les droits avant de l'exécuter.
    /// </summary>
    /// <param name="command">Identifiant de la commande, ex: "show.hints".</param>
    /// <param name="role">Rôle de l'utilisateur courant.</param>
    /// <param name="gameLevel">Niveau de la partie en cours, ou null hors partie.</param>
    /// <param name="execute">Action à exécuter si l'accès est accordé.</param>
    /// <returns>0 si succès, <see cref="ExitCodeAccessDenied"/> si refusé, code d'erreur de la commande sinon.</returns>
    public int Invoke(string command, Role role, GameLevel? gameLevel, Func<int> execute)
    {
        var result = _accessControl.CheckAccess(command, role, gameLevel);

        if (!result.IsAllowed)
        {
            WriteAccessDenied(command, result.Reason!);
            return ExitCodeAccessDenied;
        }

        return execute();
    }

    /// <summary>
    /// Version asynchrone de <see cref="Invoke"/>.
    /// </summary>
    public async Task<int> InvokeAsync(
        string command,
        Role role,
        GameLevel? gameLevel,
        Func<Task<int>> execute)
    {
        var result = _accessControl.CheckAccess(command, role, gameLevel);

        if (!result.IsAllowed)
        {
            WriteAccessDenied(command, result.Reason!);
            return ExitCodeAccessDenied;
        }

        return await execute();
    }

    private static void WriteAccessDenied(string command, string reason)
    {
        // Écriture sur stderr pour ne pas polluer la sortie standard (pipes, --output json, etc.)
        // Alias explicite pour lever l'ambiguïté avec le namespace Lama.Console
        var stderr = global::System.Console.Error;
        stderr.WriteLine($"[Accès refusé] {reason}");
        stderr.WriteLine($"Commande : {command}");
        stderr.WriteLine("Utilisez --help pour connaître les commandes disponibles.");
    }
}
