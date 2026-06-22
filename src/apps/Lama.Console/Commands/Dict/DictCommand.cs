using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Dict;

/// <summary>
/// Router pour le groupe de commandes <c>lama dict</c>.
/// Affiche l'aide du groupe si aucune sous-commande n'est reconnue.
/// Ce n'est pas une commande feuille — elle n'implémente pas <see cref="Services.ICommand"/>.
/// </summary>
public sealed class DictCommand
{
    private readonly ILogger<DictCommand> _logger;

    /// <summary>Initialise le router.</summary>
    public DictCommand(ILogger<DictCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>Affiche l'aide du groupe dict sur stdout.</summary>
    public void PrintHelp()
    {
        global::System.Console.WriteLine("Usage : lama dict <action> <arguments> [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Actions :");
        global::System.Console.WriteLine("  check <mot>                Vérifie si un mot est dans le dictionnaire");
        global::System.Console.WriteLine("  search <motif>             Recherche par motif (ex: ?OISETTE)");
        global::System.Console.WriteLine("  anagram <lettres>          Trouve des anagrammes");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Options :");
        global::System.Console.WriteLine("  --lang <code>              Langue (fr, en, ...) — défaut: fr");
        global::System.Console.WriteLine("  --min-length N             Longueur minimale (pour anagram)");
    }
}
