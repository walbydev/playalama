using Lama.Console.Services;
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
    private readonly ILogger<InteractiveMode> _logger;

    /// <summary>
    /// Initialise le mode interactif.
    /// </summary>
    public InteractiveMode(ICommandDispatcher dispatcher, ILogger<InteractiveMode> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
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

    // TODO: implémenter quand Lama.Core (cas d'usage) sera disponible
    private Task<int> HandleNewGame(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Fonctionnalité à venir (Lama.Core non implémenté).[/]");
        return Task.FromResult(ExitCodes.Success);
    }

    // TODO: implémenter quand Lama.Core (cas d'usage) sera disponible
    private Task<int> HandleJoinGame(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Fonctionnalité à venir (Lama.Core non implémenté).[/]");
        return Task.FromResult(ExitCodes.Success);
    }

    // TODO: implémenter quand Lama.Infrastructure (persistance) sera disponible
    private Task<int> HandleLoadGame(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Fonctionnalité à venir (Lama.Infrastructure non implémenté).[/]");
        return Task.FromResult(ExitCodes.Success);
    }

    // TODO: implémenter (thème, langue, etc.)
    private Task<int> HandleOptions(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Options à venir.[/]");
        return Task.FromResult(ExitCodes.Success);
    }
}
