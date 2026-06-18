using Lama.Console.Commands.Middleware;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Modes;

/// <summary>
/// Mode d'exécution commande par commande.
/// Parse les arguments, enrichit le <see cref="CommandContext"/> depuis la session
/// persistée via <see cref="ISessionService"/>, vérifie les droits via
/// <see cref="AccessControlMiddleware"/>, exécute la commande, puis se termine.
/// </summary>
public sealed class CommandLineMode : IConsoleMode
{
    private readonly string[] _args;
    private readonly ICommandDispatcher _dispatcher;
    private readonly AccessControlMiddleware _accessControl;
    private readonly ISessionService _sessionService;
    private readonly ILogger<CommandLineMode> _logger;

    /// <summary>
    /// Initialise le mode commande par commande.
    /// </summary>
    public CommandLineMode(
        string[] args,
        ICommandDispatcher dispatcher,
        AccessControlMiddleware accessControl,
        ISessionService sessionService,
        ILogger<CommandLineMode> logger)
    {
        _args = args;
        _dispatcher = dispatcher;
        _accessControl = accessControl;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Aide contextuelle (globale, groupe, commande) avant parsing complet.
        if (TryHandleHelp(_args, out var helpExitCode))
        {
            return helpExitCode;
        }

        if (_args.Length == 1 && (_args[0] == "--version" || _args[0] == "-v"))
        {
            PrintVersion();
            return ExitCodes.Success;
        }

        // Parsing + enrichissement depuis la session persistée
        var context = CommandContextParser.Parse(_args, _sessionService);

        if (context is null)
        {
            await global::System.Console.Error.WriteLineAsync(
                "Usage : lama <groupe> <action> [arguments...] [options]");
            await global::System.Console.Error.WriteLineAsync(
                "Utilisez 'lama --help' pour afficher l'aide.");
            return ExitCodes.InvalidArgument;
        }

        _logger.LogDebug(
            "Mode commande : {CommandId} | GameId={GameId} Player={PlayerName} Role={Role}",
            context.CommandId, context.GameId ?? "(aucun)",
            context.PlayerName ?? "(aucun)", context.Role);

        return await _accessControl.InvokeAsync(
            context.CommandId,
            context.Role,
            context.GameLevel,
            () => _dispatcher.DispatchAsync(context, cancellationToken));
    }

    private static bool TryHandleHelp(IReadOnlyList<string> args, out int exitCode)
    {
        exitCode = ExitCodes.Success;

        if (args.Count == 1 && IsHelpToken(args[0]))
        {
            PrintGlobalHelp();
            return true;
        }

        if (args.Count >= 1 && args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 1)
            {
                PrintGlobalHelp();
                return true;
            }

            var group = args[1].ToLowerInvariant();
            if (args.Count == 2)
            {
                if (!PrintGroupHelp(group))
                {
                    global::System.Console.Error.WriteLine($"Groupe inconnu : {group}");
                    exitCode = ExitCodes.InvalidArgument;
                }

                return true;
            }

            var action = args[2].ToLowerInvariant();
            if (!PrintCommandHelp(group, action))
            {
                global::System.Console.Error.WriteLine($"Commande inconnue : {group}.{action}");
                exitCode = ExitCodes.InvalidArgument;
            }

            return true;
        }

        if (args.Count == 2 && IsHelpToken(args[1]))
        {
            if (!PrintGroupHelp(args[0].ToLowerInvariant()))
            {
                global::System.Console.Error.WriteLine($"Groupe inconnu : {args[0]}");
                exitCode = ExitCodes.InvalidArgument;
            }

            return true;
        }

        if (args.Count >= 3 && IsHelpToken(args[2]))
        {
            if (!PrintCommandHelp(args[0].ToLowerInvariant(), args[1].ToLowerInvariant()))
            {
                global::System.Console.Error.WriteLine($"Commande inconnue : {args[0]}.{args[1]}");
                exitCode = ExitCodes.InvalidArgument;
            }

            return true;
        }

        return false;
    }

    private static bool IsHelpToken(string token) =>
        token.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static void PrintGlobalHelp()
    {
        global::System.Console.WriteLine("LAMA — Jeu de mots en console");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Usage : lama <groupe> <action> [arguments...] [options]");
        global::System.Console.WriteLine("        lama [interactive|shell|ui]");
        global::System.Console.WriteLine("        lama help [groupe] [action]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Groupes :");
        global::System.Console.WriteLine("  game        Gérer les parties (create, join, list, show, pause, save, end)");
        global::System.Console.WriteLine("  play        Jouer (move, pass, swap, challenge, check)");
        global::System.Console.WriteLine("  show        Afficher (board, rack, scores, history)");
        global::System.Console.WriteLine("  dict        Dictionnaire (check, search, anagram)");
        global::System.Console.WriteLine("  player      Profil joueur local (create)");
        global::System.Console.WriteLine("  tournament  Tournoi (create)");
        global::System.Console.WriteLine("  system      Système (setup, status, restart, account.*)");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Aide contextuelle :");
        global::System.Console.WriteLine("  lama game --help");
        global::System.Console.WriteLine("  lama play move --help");
        global::System.Console.WriteLine("  lama help system restart");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Options globales :");
        global::System.Console.WriteLine("  -h, --help              Aide contextuelle");
        global::System.Console.WriteLine("  -v, --version           Version du jeu");
        global::System.Console.WriteLine("  -V, --verbose           Mode verbeux");
        global::System.Console.WriteLine("  -q, --quiet             Mode silencieux");
        global::System.Console.WriteLine("      --no-color          Désactive les couleurs ANSI");
        global::System.Console.WriteLine("      --high-contrast     Mode contraste élevé");
        global::System.Console.WriteLine("  -l, --lang <code>       Langue (fr, en, de, es, it)");
        global::System.Console.WriteLine("  -o, --output <fmt>      Format de sortie (text, json, csv)");
        global::System.Console.WriteLine("      --game-id <id>      Surcharge l'identifiant de partie (session)");
        global::System.Console.WriteLine("      --player <id>       Surcharge l'identifiant joueur (session)");
    }

    private static bool PrintGroupHelp(string group)
    {
        switch (group)
        {
            case "game":
                global::System.Console.WriteLine("Usage : lama game <action> [arguments...] [options]");
                global::System.Console.WriteLine("Actions : create, join, list, show, pause, save, end");
                global::System.Console.WriteLine("Exemples :");
                global::System.Console.WriteLine("  lama game create Alice --level standard");
                global::System.Console.WriteLine("  lama game show --output json");
                return true;

            case "play":
                global::System.Console.WriteLine("Usage : lama play <action> [arguments...] [options]");
                global::System.Console.WriteLine("Actions : move, pass, swap, challenge, check");
                global::System.Console.WriteLine("Exemples :");
                global::System.Console.WriteLine("  lama play move H8 LAMA H");
                global::System.Console.WriteLine("  lama play swap --all");
                return true;

            case "show":
                global::System.Console.WriteLine("Usage : lama show <action> [options]");
                global::System.Console.WriteLine("Actions : board, rack, scores, history");
                global::System.Console.WriteLine("Exemples :");
                global::System.Console.WriteLine("  lama show board");
                global::System.Console.WriteLine("  lama show history --output csv --last 10");
                return true;

            case "dict":
                global::System.Console.WriteLine("Usage : lama dict <action> <arguments> [options]");
                global::System.Console.WriteLine("Actions : check, search, anagram");
                global::System.Console.WriteLine("Exemples :");
                global::System.Console.WriteLine("  lama dict check lama");
                global::System.Console.WriteLine("  lama dict anagram lam --min-length 2");
                return true;

            case "player":
                global::System.Console.WriteLine("Usage : lama player create <nom>");
                global::System.Console.WriteLine("Crée un profil local hors partie (session locale). ");
                return true;

            case "tournament":
                global::System.Console.WriteLine("Usage : lama tournament create <nom> [--host <nom>]");
                global::System.Console.WriteLine("Crée une partie de niveau Tournament et prépare la session hôte.");
                return true;

            case "system":
                global::System.Console.WriteLine("Usage : lama system <action> [options]");
                global::System.Console.WriteLine("Actions : setup, status, restart, account.create, account.list, account.revoke");
                global::System.Console.WriteLine("Exemples :");
                global::System.Console.WriteLine("  lama system status --output json");
                global::System.Console.WriteLine("  lama system restart");
                return true;

            default:
                return false;
        }
    }

    private static bool PrintCommandHelp(string group, string action)
    {
        if (group == "system" && action == "restart")
        {
            global::System.Console.WriteLine("Usage : lama system restart");
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Effectue un redémarrage logique in-process :");
            global::System.Console.WriteLine("- vide le cache mémoire des sessions de jeu");
            global::System.Console.WriteLine("- conserve les données persistées");
            global::System.Console.WriteLine("- tente de recharger la partie active depuis la session");
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Notes : cette commande ne redémarre pas un service OS externe.");
            return true;
        }

        if (group == "play" && action == "move")
        {
            global::System.Console.WriteLine("Usage : lama play move <case> <mot> <direction>");
            global::System.Console.WriteLine("Exemple : lama play move H8 LAMA H");
            global::System.Console.WriteLine("Direction : H (horizontal) ou V (vertical)");
            return true;
        }

        if (group == "game" && action == "create")
        {
            global::System.Console.WriteLine("Usage : lama game create [<hote>] [--level casual|standard|competitive|tournament]");
            global::System.Console.WriteLine("Crée une nouvelle partie et initialise la session locale.");
            return true;
        }

        if (group == "system" && action == "status")
        {
            global::System.Console.WriteLine("Usage : lama system status [--output text|json|csv]");
            global::System.Console.WriteLine("Affiche l'état système: initialisation, comptes, parties persistées, session.");
            return true;
        }

        if (group == "player" && action == "create")
        {
            global::System.Console.WriteLine("Usage : lama player create <nom>");
            global::System.Console.WriteLine("Crée un profil joueur local (hors partie active).");
            return true;
        }

        if (group == "tournament" && action == "create")
        {
            global::System.Console.WriteLine("Usage : lama tournament create <nom> [--host <nom>]");
            global::System.Console.WriteLine("Crée une partie niveau Tournament et met à jour la session hôte.");
            return true;
        }

        return false;
    }

    private static void PrintVersion()
    {
        var version = typeof(CommandLineMode).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        global::System.Console.WriteLine($"lama {version}");
    }
}
