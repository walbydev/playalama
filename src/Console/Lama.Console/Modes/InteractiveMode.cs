using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Lama.Console.Modes;

/// <summary>
/// Mode interactif textuel — expérience principale de jeu en console.
/// Lance une boucle de menus et de prompts via Spectre.Console.
/// Ne contient aucune logique de jeu ; délègue aux services applicatifs.
/// </summary>
public sealed class InteractiveMode : IConsoleMode
{
    private readonly ICommandDispatcher _dispatcher;
    private readonly ISessionService _sessionService;
    private readonly ILogger<InteractiveMode> _logger;

    /// <summary>
    /// Initialise le mode interactif.
    /// </summary>
    public InteractiveMode(
        ICommandDispatcher dispatcher,
        ISessionService sessionService,
        ILogger<InteractiveMode> logger)
    {
        _dispatcher     = dispatcher;
        _sessionService = sessionService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Démarrage du mode interactif");

        AnsiConsole.Write(new FigletText("LAMA").Color(Color.Green));
        AnsiConsole.MarkupLine("[grey]Jeu de mots inspiré du Scrabble — .NET 10[/]");
        AnsiConsole.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Menu principal[/]")
                    .AddChoices(
                        "Nouvelle partie",
                        "Rejoindre une partie",
                        "Charger une partie",
                        "Options",
                        "Quitter"));

            var exitCode = choice switch
            {
                "Nouvelle partie"       => await HandleNewGame(cancellationToken),
                "Rejoindre une partie"  => await HandleJoinGame(cancellationToken),
                "Charger une partie"    => await HandleLoadGame(cancellationToken),
                "Options"               => await HandleOptions(cancellationToken),
                "Quitter"               => ExitCodes.Success,
                _                       => ExitCodes.InvalidArgument
            };

            if (choice == "Quitter")
                break;

            if (exitCode != ExitCodes.Success)
                _logger.LogWarning("Action '{Choice}' terminée avec le code {ExitCode}",
                    choice, exitCode);
        }

        AnsiConsole.MarkupLine("[grey]À bientôt ![/]");
        return ExitCodes.Success;
    }

    private async Task<int> HandleNewGame(CancellationToken cancellationToken)
    {
        var defaultName = _sessionService.LoadSession()?.PlayerName ?? "Hôte";
        var hostName = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Nom de l'hôte :[/]")
                .DefaultValue(defaultName)
                .Validate(name => !string.IsNullOrWhiteSpace(name)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Le nom ne peut pas être vide.")));

        var level = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Niveau de partie[/]")
                .AddChoices("casual", "standard", "competitive", "tournament"));

        var context = new CommandContext
        {
            Group = "game",
            Action = "create",
            CommandId = "game.create",
            Arguments = [hostName],
            Options = new Dictionary<string, string?> { ["level"] = level }
        };

        return await _dispatcher.DispatchAsync(context, cancellationToken);
    }

    private async Task<int> HandleJoinGame(CancellationToken cancellationToken)
    {
        var defaultName = _sessionService.LoadSession()?.PlayerName ?? "Joueur";
        var playerName = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Nom du joueur :[/]")
                .DefaultValue(defaultName)
                .Validate(name => !string.IsNullOrWhiteSpace(name)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Le nom ne peut pas être vide.")));

        var context = new CommandContext
        {
            Group = "game",
            Action = "join",
            CommandId = "game.join",
            Arguments = [playerName]
        };

        return await _dispatcher.DispatchAsync(context, cancellationToken);
    }

    private async Task<int> HandleLoadGame(CancellationToken cancellationToken)
    {
        var gameId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]ID de partie à afficher :[/]")
                .Validate(id => !string.IsNullOrWhiteSpace(id)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("L'ID ne peut pas être vide.")));

        var context = new CommandContext
        {
            Group = "game",
            Action = "show",
            CommandId = "game.show",
            Options = new Dictionary<string, string?>
            {
                ["game-id"] = gameId,
                ["output"] = "text"
            }
        };

        return await _dispatcher.DispatchAsync(context, cancellationToken);
    }

    private Task<int> HandleOptions(CancellationToken cancellationToken)
    {
        var session = _sessionService.LoadSession();
        AnsiConsole.MarkupLine("[green]Session locale[/]");
        AnsiConsole.MarkupLine($"- Fichier : [grey]{_sessionService.SessionFilePath}[/]");
        if (session is null)
        {
            AnsiConsole.MarkupLine("- Etat    : [yellow]aucune session active[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"- Joueur  : [white]{session.PlayerName ?? "(aucun)"}[/]");
            AnsiConsole.MarkupLine($"- Role    : [white]{session.Role}[/]");
            AnsiConsole.MarkupLine($"- Partie  : [white]{session.GameId ?? "(aucune)"}[/]");
            AnsiConsole.MarkupLine($"- MAJ     : [grey]{session.UpdatedAt:O}[/]");
        }
        return Task.FromResult(ExitCodes.Success);
    }
}
