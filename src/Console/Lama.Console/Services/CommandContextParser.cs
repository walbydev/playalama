using Lama.Contracts;

namespace Lama.Console.Services;

/// <summary>
/// Parse les arguments de la ligne de commande en un <see cref="CommandContext"/>.
/// Le format attendu est : lama &lt;groupe&gt; &lt;action&gt; [arguments...] [--option valeur]
///
/// Résolution du contexte de session (GameId, PlayerId, Role, GameLevel) :
/// <list type="number">
///   <item>Charge la session persistée via <see cref="ISessionService"/>.</item>
///   <item>Les options CLI <c>--game-id</c> et <c>--player</c> surchargent la session.</item>
/// </list>
/// </summary>
public static class CommandContextParser
{
    /// <summary>
    /// Parse un tableau d'arguments et enrichit le contexte depuis la session persistée.
    /// </summary>
    /// <param name="args">Les arguments de la ligne de commande (sans le nom du programme).</param>
    /// <param name="sessionService">
    /// Service de session pour charger le contexte de partie en cours.
    /// </param>
    /// <returns>Le <see cref="CommandContext"/> enrichi, ou null si les arguments sont invalides.</returns>
    public static CommandContext? Parse(string[] args, ISessionService sessionService)
    {
        if (args.Length < 2)
            return null;

        var group  = args[0].ToLowerInvariant();
        var action = args[1].ToLowerInvariant();

        var positionalArgs = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--"))
            {
                var key = arg[2..];
                // Option avec valeur : --option valeur (la valeur ne commence pas par -)
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    options[key] = args[++i];
                else
                    options[key] = null; // Flag booléen
            }
            else if (arg.StartsWith('-') && arg.Length == 2)
            {
                var key = arg[1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    options[key] = args[++i];
                else
                    options[key] = null;
            }
            else
            {
                positionalArgs.Add(arg);
            }
        }

        // ─── Résolution du contexte de session ──────────────────────────────
        // 1. Charger la session persistée (null si aucune partie active)
        var session = sessionService.LoadSession();

        // 2. Les options CLI --game-id et --player surchargent la session
        var gameId = options.TryGetValue("game-id", out var gid) && gid is not null
            ? gid
            : session?.GameId;

        var playerId = options.TryGetValue("player", out var pid) && pid is not null
            ? pid
            : session?.PlayerId;

        // Le Role et le GameLevel viennent exclusivement de la session persistée
        // (ils ne peuvent pas être surchargés par CLI pour des raisons de sécurité)
        var role      = session?.Role      ?? Role.Player;
        var gameLevel = session?.GameLevel;
        var playerName = session?.PlayerName;

        // ─── Cas particulier : game create et game join n'ont pas de session ─
        // Ces commandes sont appelées sans session active, c'est normal.
        // Elles vont créer/écrire la session elles-mêmes.

        return new CommandContext
        {
            Group       = group,
            Action      = action,
            Arguments   = positionalArgs,
            Options     = options,
            GameId      = gameId,
            PlayerId    = playerId,
            PlayerName  = playerName,
            Role        = role,
            GameLevel   = gameLevel
        };
    }
}
