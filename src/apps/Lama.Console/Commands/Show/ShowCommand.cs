using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Router pour le groupe de commandes <c>lama show</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class ShowCommand
{
    private readonly ILogger<ShowCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public ShowCommand(ILogger<ShowCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe show sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama show <action> [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  board     Affiche le plateau de jeu");
        global::System.Console.WriteLine("  rack      Affiche le rack du joueur courant");
        global::System.Console.WriteLine("  scores    Affiche le tableau des scores");
        global::System.Console.WriteLine("  history   Affiche l'historique des coups joués");
    }
}
