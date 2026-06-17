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
        // Traitement de --help et --version avant le parsing complet
        if (_args.Length == 1 && (_args[0] == "--help" || _args[0] == "-h"))
        {
            PrintHelp();
            return ExitCodes.Success;
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

    private static void PrintHelp()
    {
        global::System.Console.WriteLine("LAMA — Jeu de mots en console");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Usage : lama <groupe> <action> [arguments...] [options]");
        global::System.Console.WriteLine("        lama [interactive|shell|ui]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Groupes :");
        global::System.Console.WriteLine("  game        Gérer les parties (create, join, list, show, pause, save, end)");
        global::System.Console.WriteLine("  play        Jouer (move, pass, swap, challenge, check)");
        global::System.Console.WriteLine("  show        Afficher (board, rack, scores, history)");
        global::System.Console.WriteLine("  dict        Dictionnaire (check, search, anagram)");
        global::System.Console.WriteLine("  player      Joueurs (create)");
        global::System.Console.WriteLine("  tournament  Tournois (create)");
        global::System.Console.WriteLine("  system      Système (status, restart)");
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

    private static void PrintVersion()
    {
        var version = typeof(CommandLineMode).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        global::System.Console.WriteLine($"lama {version}");
    }
}
