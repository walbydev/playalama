using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Router pour le groupe de commandes <c>lama system</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// Toutes les commandes system sont réservées aux administrateurs.
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
        global::System.Console.WriteLine("Actions (réservées aux administrateurs) :");
        global::System.Console.WriteLine("  status     Affiche l'état du système");
        global::System.Console.WriteLine("  restart    Redémarre le service");
        global::System.Console.WriteLine("  clean      Nettoie toutes les parties actives");
    }
}
