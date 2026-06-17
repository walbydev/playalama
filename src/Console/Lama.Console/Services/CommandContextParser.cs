using Lama.Contracts;

namespace Lama.Console.Services;

/// <summary>
/// Parse les arguments de la ligne de commande en un <see cref="CommandContext"/>.
/// Le format attendu est : lama &lt;groupe&gt; &lt;action&gt; [arguments...] [--option valeur]
/// </summary>
public static class CommandContextParser
{
    /// <summary>
    /// Parse un tableau d'arguments en CommandContext.
    /// </summary>
    /// <param name="args">Les arguments de la ligne de commande (sans le nom du programme).</param>
    /// <returns>Le CommandContext parsé, ou null si les arguments sont invalides.</returns>
    public static CommandContext? Parse(string[] args)
    {
        if (args.Length < 2)
            return null;

        var group = args[0].ToLowerInvariant();
        var action = args[1].ToLowerInvariant();

        var positionalArgs = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--"))
            {
                var key = arg[2..];
                // Vérifier si l'option a une valeur (--option valeur)
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    options[key] = args[++i];
                }
                else
                {
                    options[key] = null; // Flag booléen
                }
            }
            else if (arg.StartsWith('-') && arg.Length == 2)
            {
                var key = arg[1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    options[key] = args[++i];
                }
                else
                {
                    options[key] = null;
                }
            }
            else
            {
                positionalArgs.Add(arg);
            }
        }

        return new CommandContext
        {
            Group = group,
            Action = action,
            Arguments = positionalArgs,
            Options = options,
            Role = Role.Player, // TODO: résoudre depuis la session/config quand implémenté
            GameLevel = null    // TODO: résoudre depuis l'état de la partie quand implémenté
        };
    }
}
