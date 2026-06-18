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
                    if (!PrintSingleLevelCommandHelp(group))
                    {
                        global::System.Console.Error.WriteLine($"Groupe inconnu : {group}");
                        exitCode = ExitCodes.InvalidArgument;
                    }
                }

                return true;
            }

            var actionPath = string.Join(' ', args.Skip(2)).ToLowerInvariant();
            if (!PrintCommandHelp(group, actionPath))
            {
                global::System.Console.Error.WriteLine($"Commande inconnue : {group} {actionPath}");
                exitCode = ExitCodes.InvalidArgument;
            }

            return true;
        }

        if (args.Count == 2 && IsHelpToken(args[1]))
        {
            var token = args[0].ToLowerInvariant();
            if (!PrintGroupHelp(token) && !PrintSingleLevelCommandHelp(token))
            {
                global::System.Console.Error.WriteLine($"Groupe inconnu : {args[0]}");
                exitCode = ExitCodes.InvalidArgument;
            }

            return true;
        }

        if (args.Count >= 3 && IsHelpToken(args[^1]))
        {
            var group = args[0].ToLowerInvariant();
            var actionPath = string.Join(' ', args.Skip(1).Take(args.Count - 2)).ToLowerInvariant();
            if (!PrintCommandHelp(group, actionPath))
            {
                global::System.Console.Error.WriteLine($"Commande inconnue : {group} {actionPath}");
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
        foreach (var group in HelpCatalog.Groups)
            global::System.Console.WriteLine(
                $"  {group.Group,-10} {group.Summary} ({group.ActionsSummary})");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Aide contextuelle :");
        global::System.Console.WriteLine("  lama game --help");
        global::System.Console.WriteLine("  lama play move --help");
        global::System.Console.WriteLine("  lama system account create --help");
        global::System.Console.WriteLine("  lama help system restart");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Options globales :");
        foreach (var option in HelpCatalog.GlobalOptions)
            global::System.Console.WriteLine($"  {option.Name,-24} {option.Description}");
    }

    private static bool PrintGroupHelp(string group)
    {
        if (!HelpCatalog.TryGetGroup(group, out var helpGroup) || helpGroup is null)
            return false;

        global::System.Console.WriteLine($"Usage : lama {helpGroup.Group} <action> [arguments...] [options]");
        global::System.Console.WriteLine(helpGroup.Summary);
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Commandes :");
        foreach (var cmd in HelpCatalog.GetGroupCommands(helpGroup.Group))
            global::System.Console.WriteLine($"  {cmd.ActionPath,-16} {cmd.Description}");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine($"Exemple : lama help {helpGroup.Group} <action>");
        return true;
    }

    private static bool PrintCommandHelp(string group, string actionPath)
    {
        if (!HelpCatalog.TryGetCommand(group, actionPath, out var command) || command is null)
            return false;

        global::System.Console.WriteLine($"Usage : {command.Usage}");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine(command.Description);
        global::System.Console.WriteLine();
        global::System.Console.WriteLine($"ACL            : {command.AllowedRoles}");
        global::System.Console.WriteLine($"Formats sortie : {command.OutputFormats}");

        if (command.Options.Count > 0)
        {
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Options :");
            foreach (var option in command.Options)
                global::System.Console.WriteLine($"  {option.Name,-16} {option.Description}");
        }

        if (command.Examples.Count > 0)
        {
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Exemples :");
            foreach (var example in command.Examples)
                global::System.Console.WriteLine($"  {example}");
        }

        if (command.Notes is { Count: > 0 })
        {
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Notes :");
            foreach (var note in command.Notes)
                global::System.Console.WriteLine($"- {note}");
        }

        return true;
    }

    private static bool PrintSingleLevelCommandHelp(string commandId)
    {
        if (!HelpCatalog.TryGetSingleLevelCommand(commandId, out var command) || command is null)
            return false;

        return PrintCommandHelp(command.Group, command.ActionPath);
    }

    private static void PrintVersion()
    {
        var version = typeof(CommandLineMode).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        global::System.Console.WriteLine($"lama {version}");
    }
}
