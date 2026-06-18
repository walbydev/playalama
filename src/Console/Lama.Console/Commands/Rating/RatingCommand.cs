using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Rating;

/// <summary>
/// Router pour le groupe de commandes <c>lama rating</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class RatingCommand
{
    private readonly ILogger<RatingCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public RatingCommand(ILogger<RatingCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe rating sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama rating <action> [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  show         Affiche le rating d'un joueur");
        global::System.Console.WriteLine("  leaderboard  Affiche le classement mondial");
        global::System.Console.WriteLine("  stats        Affiche les statistiques d'un joueur");
    }
}

