using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Player;

/// <summary>
/// Router pour le groupe de commandes <c>lama player</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class PlayerCommand
{
    private readonly ILogger<PlayerCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public PlayerCommand(ILogger<PlayerCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe player sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama player <action> [arguments...] [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  create <nom>    Cree un profil joueur");
        global::System.Console.WriteLine("  show [id]       Affiche profil + rating + stats");
        global::System.Console.WriteLine("  update [id]     Met a jour pseudo/pays/region/naissance");
    }
}
