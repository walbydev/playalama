using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Router pour le groupe de commandes <c>lama play</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class PlayCommand
{
    private readonly ILogger<PlayCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public PlayCommand(ILogger<PlayCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe play sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama play <action> [arguments...] [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  move <case> <mot> <direction>   Pose un mot (ex: H8 MAISON H)");
        global::System.Console.WriteLine("  pass                            Passe son tour");
        global::System.Console.WriteLine("  swap <lettres>                  Échange des lettres avec le sac");
        global::System.Console.WriteLine("  challenge                       Conteste le dernier mot joué");
        global::System.Console.WriteLine("  check                           Vérifie un coup avant de le jouer (Casual uniquement)");
    }
}
