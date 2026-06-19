using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Router pour le groupe de commandes <c>lama system</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// La plupart des commandes system sont réservées aux administrateurs,
/// avec exception pour la configuration runtime serveur (show/clear).
/// </summary>
public sealed class SystemCommand
{
    private readonly ILogger<SystemCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public SystemCommand(ILogger<SystemCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe system sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama system <action> [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  status     Affiche l'état du système");
        global::System.Console.WriteLine("  restart    Redémarre le service");
        global::System.Console.WriteLine("  clean      Nettoie toutes les parties actives");
        global::System.Console.WriteLine("  server show   Affiche la cible runtime (local/online)");
        global::System.Console.WriteLine("  server clear  Efface l'URL serveur persistée");
    }
}
