using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Tournament;

/// <summary>
/// Router pour le groupe de commandes <c>lama tournament</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class TournamentCommand
{
    private readonly ILogger<TournamentCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public TournamentCommand(ILogger<TournamentCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe tournament sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama tournament <action> [arguments...] [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  create <nom>    Crée un tournoi (admin)");
    }
}
