using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Router pour le groupe de commandes <c>lama game</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class GameCommand
{
    private readonly ILogger<GameCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public GameCommand(ILogger<GameCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe game sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama game <action> [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  create    Crée une nouvelle partie (admin)");
        global::System.Console.WriteLine("  join      Rejoint une partie existante");
        global::System.Console.WriteLine("  list      Liste les parties disponibles");
        global::System.Console.WriteLine("  show      Affiche les informations d'une partie");
        global::System.Console.WriteLine("  pause     Met en pause la partie");
        global::System.Console.WriteLine("  save      Sauvegarde la partie");
        global::System.Console.WriteLine("  end       Termine la partie");
    }
}
