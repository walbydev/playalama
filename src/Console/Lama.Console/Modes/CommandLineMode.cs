using Lama.Console.Commands.Middleware;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Modes;

/// <summary>
/// Mode d'exécution commande par commande.
/// Parse les arguments, construit un <see cref="CommandContext"/>, vérifie les droits
/// via <see cref="AccessControlMiddleware"/>, exécute la commande, puis se termine.
/// </summary>
public sealed class CommandLineMode : IConsoleMode
{
    private readonly string[] _args;
    private readonly ICommandDispatcher _dispatcher;
    private readonly AccessControlMiddleware _accessControl;
    private readonly ILogger<CommandLineMode> _logger;

    /// <summary>
    /// Initialise le mode commande par commande.
    /// </summary>
    public CommandLineMode(
        string[] args,
        ICommandDispatcher dispatcher,
        AccessControlMiddleware accessControl,
        ILogger<CommandLineMode> logger)
    {
        _args = args;
        _dispatcher = dispatcher;
        _accessControl = accessControl;
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

        var context = CommandContextParser.Parse(_args);

        if (context is null)
        {
            await System.Console.Error.WriteLineAsync(
                "Usage : lama <groupe> <action> [arguments...] [options]");
            await System.Console.Error.WriteLineAsync(
                "Utilisez 'lama --help' pour afficher l'aide.");
            return ExitCodes.InvalidArgument;
        }

        _logger.LogDebug("Mode commande : {CommandId}", context.CommandId);

        return await _accessControl.InvokeAsync(
            context.CommandId,
            context.Role,
            context.GameLevel,
            () => _dispatcher.DispatchAsync(context, cancellationToken));
    }

    private static void PrintHelp()
    {
        System.Console.WriteLine("LAMA — Jeu de mots en console");
        System.Console.WriteLine();
        System.Console.WriteLine("Usage : lama <groupe> <action> [arguments...] [options]");
        System.Console.WriteLine("        lama [interactive|shell|ui]");
        System.Console.WriteLine();
        System.Console.WriteLine("Groupes :");
        System.Console.WriteLine("  game        Gérer les parties (create, join, list, show, pause, save, end)");
        System.Console.WriteLine("  play        Jouer (move, pass, swap, challenge, check)");
        System.Console.WriteLine("  show        Afficher (board, rack, scores, history)");
        System.Console.WriteLine("  dict        Dictionnaire (check, search, anagram)");
        System.Console.WriteLine("  player      Joueurs (create)");
        System.Console.WriteLine("  tournament  Tournois (create)");
        System.Console.WriteLine("  system      Système (status, restart)");
        System.Console.WriteLine();
        System.Console.WriteLine("Options globales :");
        System.Console.WriteLine("  -h, --help          Aide contextuelle");
        System.Console.WriteLine("  -v, --version       Version du jeu");
        System.Console.WriteLine("  -V, --verbose       Mode verbeux");
        System.Console.WriteLine("  -q, --quiet         Mode silencieux");
        System.Console.WriteLine("      --no-color      Désactive les couleurs ANSI");
        System.Console.WriteLine("      --high-contrast Mode contraste élevé");
        System.Console.WriteLine("  -l, --lang <code>   Langue (fr, en, de, es, it)");
        System.Console.WriteLine("  -o, --output <fmt>  Format de sortie (text, json, csv)");
    }

    private static void PrintVersion()
    {
        var version = typeof(CommandLineMode).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        System.Console.WriteLine($"lama {version}");
    }
}
