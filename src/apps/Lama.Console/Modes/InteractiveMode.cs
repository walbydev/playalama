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
    private readonly RuntimeModeService _runtimeMode;
    private readonly ILogger<InteractiveMode> _logger;

    /// <summary>
    /// Initialise le mode interactif.
    /// </summary>
    public InteractiveMode(
        ICommandDispatcher dispatcher,
        ISessionService sessionService,
        RuntimeModeService runtimeMode,
        ILogger<InteractiveMode> logger)
    {
        _dispatcher     = dispatcher;
        _sessionService = sessionService;
        _runtimeMode    = runtimeMode;
        _logger         = logger;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Démarrage du mode interactif");

        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            global::System.Console.Error.WriteLine(
                "[interactive] Ce terminal n'est pas interactif (TTY requis). " +
                "Utilisez le mode commande par commande (ex: lama game create ...)."
            );
            return ExitCodes.InvalidArgument;
        }

        AnsiConsole.Write(new FigletText("LAMA").Color(Color.Green));
        AnsiConsole.MarkupLine("[grey]Jeu de mots inspiré du Scrabble — .NET 10[/]");
        AnsiConsole.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            var active = _sessionService.LoadSession();
            var runtimeTarget = _runtimeMode.IsOnline
                ? $"online ({_runtimeMode.ServerBaseUrl})"
                : "local";
            var subtitle = active?.GameId is null
                ? $"[grey]Mode: {runtimeTarget} | Aucune partie active[/]"
                : $"[grey]Mode: {runtimeTarget} | Partie: {active.GameId[..Math.Min(8, active.GameId.Length)]} | Joueur: {active.PlayerName ?? "?"} | Role: {active.Role}[/]";

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[green]Menu principal[/]\n{subtitle}")
                    .AddChoices(
                        "Nouvelle partie",
                        "Rejoindre une partie",
                        "Demarrer la partie",
                        "Jouer un tour",
                        "Charger une partie",
                        "Options",
                        "Reafficher le dashboard",
                        "Effacer la session locale",
                        "Quitter"));

            var exitCode = choice switch
            {
                "Nouvelle partie"       => await HandleNewGame(cancellationToken),
                "Rejoindre une partie"  => await HandleJoinGame(cancellationToken),
                "Demarrer la partie"    => await HandleStartGame(cancellationToken),
                "Jouer un tour"         => await HandlePlayTurn(cancellationToken),
                "Charger une partie"    => await HandleLoadGame(cancellationToken),
                "Options"               => await HandleOptions(cancellationToken),
                "Reafficher le dashboard" => await HandleDashboard(cancellationToken),
                "Effacer la session locale" => HandleClearSession(),
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

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Mode de partie[/]")
                .AddChoices("solo", "multi"));

        if (mode == "multi" && !_runtimeMode.IsOnline)
        {
            AnsiConsole.MarkupLine("[yellow]Le mode multi est disponible uniquement en online (serveur).[/]");
            return ExitCodes.InvalidArgument;
        }

        var options = new Dictionary<string, string?>
        {
            ["level"] = level,
            ["mode"] = mode
        };

        var gameName = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Nom de partie (laisser vide = auto) :[/]")
                .AllowEmpty());
        if (!string.IsNullOrWhiteSpace(gameName))
            options["name"] = gameName;

        var enableAi = AnsiConsole.Confirm("[green]Ajouter une IA ?[/]", defaultValue: false);
        if (enableAi)
            options["with-ai"] = null;

        if (mode == "multi")
        {
            var maxPlayers = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Nombre max de participants (IA incluse)[/]")
                    .AddChoices("2", "3", "4"));
            options["max-players"] = maxPlayers;

            var isPrivate = AnsiConsole.Confirm("[green]Partie privée (mot de passe) ?[/]", defaultValue: false);
            if (isPrivate)
            {
                options["private"] = null;
                var password = AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]Mot de passe :[/]")
                        .Secret()
                        .Validate(value => !string.IsNullOrWhiteSpace(value)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Le mot de passe est requis.")));
                options["password"] = password;
            }
        }

        var context = new CommandContext
        {
            Group = "game",
            Action = "create",
            CommandId = "game.create",
            Arguments = [hostName],
            Options = options
        };

        return await _dispatcher.DispatchAsync(context, cancellationToken);
    }

    private async Task<int> HandleStartGame(CancellationToken cancellationToken)
    {
        var session = _sessionService.LoadSession();
        if (session?.GameId is null || session.PlayerId is null)
        {
            AnsiConsole.MarkupLine("[yellow]Aucune partie active à démarrer.[/]");
            return ExitCodes.GameNotFound;
        }

        var context = BuildSessionBoundContext("game", "start", "game.start", session);
        return await _dispatcher.DispatchAsync(context, cancellationToken);
    }

    private async Task<int> HandleJoinGame(CancellationToken cancellationToken)
    {
        var session = _sessionService.LoadSession();

        if (_runtimeMode.IsOnline)
        {
            var gameId = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]ID de la partie à rejoindre :[/]")
                    .Validate(id => !string.IsNullOrWhiteSpace(id)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("L'ID de partie est requis.")));

            var onlineDefaultName = session?.PlayerName ?? "Joueur";
            var onlinePlayerName = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Nom du joueur :[/]")
                    .DefaultValue(onlineDefaultName)
                    .Validate(name => !string.IsNullOrWhiteSpace(name)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Le nom ne peut pas être vide.")));

            var isPrivate = AnsiConsole.Confirm("[green]Partie privée (mot de passe) ?[/]", defaultValue: false);
            var options = new Dictionary<string, string?> { ["game-id"] = gameId };
            if (isPrivate)
            {
                var password = AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]Mot de passe :[/]")
                        .Secret()
                        .Validate(value => !string.IsNullOrWhiteSpace(value)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("Le mot de passe est requis.")));
                options["password"] = password;
            }

            var onlineContext = new CommandContext
            {
                Group = "game",
                Action = "join",
                CommandId = "game.join",
                Arguments = [onlinePlayerName],
                Options = options,
                GameId = gameId,
                PlayerId = session?.PlayerId,
                PlayerName = session?.PlayerName,
                Role = session?.Role ?? Role.Player,
                GameLevel = session?.GameLevel
            };

            return await _dispatcher.DispatchAsync(onlineContext, cancellationToken);
        }

        if (session?.GameId is null || session.PlayerId is null)
        {
            AnsiConsole.MarkupLine("[yellow]Aucune partie active a rejoindre dans cette session. Creez d'abord une partie.[/]");
            return ExitCodes.GameNotFound;
        }

        var defaultName = _sessionService.LoadSession()?.PlayerName ?? "Joueur";
        var playerName = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Nom du joueur :[/]")
                .DefaultValue(defaultName)
                .Validate(name => !string.IsNullOrWhiteSpace(name)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Le nom ne peut pas être vide.")));

        var context = BuildSessionBoundContext("game", "join", "game.join", session, [playerName]);

        return await _dispatcher.DispatchAsync(context, cancellationToken);
    }

    private async Task<int> HandlePlayTurn(CancellationToken cancellationToken)
    {
        var session = _sessionService.LoadSession();
        if (session?.GameId is null || session.PlayerId is null)
        {
            AnsiConsole.MarkupLine("[yellow]Aucune partie active. Créez ou rejoignez une partie d'abord.[/]");
            return ExitCodes.GameNotFound;
        }

        AnsiConsole.MarkupLine("[grey]Mode tour continu actif. Choisissez une action (Retour pour quitter ce mode).[/]");
        await RenderTurnDashboard(session, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Action de tour[/]")
                    .AddChoices(
                        "Jouer un mot",
                        "Verifier un coup",
                        "Contester (challenge)",
                        "Passer",
                        "Echanger",
                        "Reafficher le dashboard",
                        "Abandonner la partie",
                        "Retour"));

            if (action == "Retour")
                return ExitCodes.Success;

            if (action == "Reafficher le dashboard")
            {
                await RenderTurnDashboard(session, cancellationToken);
                continue;
            }

            if (action == "Abandonner la partie")
            {
                var confirm = AnsiConsole.Confirm("[yellow]Confirmer l'abandon et terminer la partie ?[/]", defaultValue: false);
                if (!confirm)
                    continue;

                var endContext = BuildSessionBoundContext("game", "end", "game.end", session);
                var endCode = await _dispatcher.DispatchAsync(endContext, cancellationToken);
                if (endCode == ExitCodes.Success)
                {
                    AnsiConsole.MarkupLine("[green]Partie terminee. Retour au menu principal.[/]");
                    return ExitCodes.Success;
                }

                AnsiConsole.MarkupLine($"[yellow]Impossible de terminer la partie (code {endCode}).[/]");
                continue;
            }

            // Recharge la session a chaque tour pour suivre les evolutions eventuelles.
            session = _sessionService.LoadSession();
            if (session?.GameId is null || session.PlayerId is null)
            {
                AnsiConsole.MarkupLine("[yellow]Session de partie indisponible. Retour au menu principal.[/]");
                return ExitCodes.GameNotFound;
            }

            CommandContext? context = action switch
            {
                "Passer" => BuildSessionBoundContext("play", "pass", "play.pass", session),
                "Jouer un mot" => BuildMoveContext(session),
                "Verifier un coup" => BuildCheckContext(session),
                "Contester (challenge)" => BuildSessionBoundContext("play", "challenge", "play.challenge", session),
                "Echanger" => BuildSwapContext(session),
                _ => null
            };

            if (context is null)
                return ExitCodes.InvalidArgument;

            var exitCode = await _dispatcher.DispatchAsync(context, cancellationToken);
            if (exitCode == ExitCodes.Success)
            {
                await RenderTurnDashboard(session, cancellationToken);
                continue;
            }

            if (exitCode == ExitCodes.GameNotFound)
            {
                AnsiConsole.MarkupLine("[yellow]Partie indisponible. Retour au menu principal.[/]");
                return exitCode;
            }

            AnsiConsole.MarkupLine($"[yellow]Action terminee avec le code {exitCode}. Reessayez ou consultez 'Verifier un coup'.[/]");
        }

        return ExitCodes.Success;
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

    private async Task<int> HandleOptions(CancellationToken cancellationToken)
    {
        var session = _sessionService.LoadSession();
        while (!cancellationToken.IsCancellationRequested)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Options[/]")
                    .AddChoices(
                        "Afficher la session locale",
                        "Mon profil",
                        "Annuaire joueurs",
                        "Mon rating",
                        "Mes stats (30 jours)",
                        "Top 10 mondial",
                        "Retour"));

            if (choice == "Retour")
                return ExitCodes.Success;

            switch (choice)
            {
                case "Afficher la session locale":
                    AnsiConsole.MarkupLine("[green]Session locale[/]");
                    AnsiConsole.MarkupLine($"- Fichier : [grey]{_sessionService.SessionFilePath}[/]");
                    AnsiConsole.MarkupLine($"- Runtime : [white]{(_runtimeMode.IsOnline ? $"online ({_runtimeMode.ServerBaseUrl})" : "local")}[/]");
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
                    break;

                case "Mon profil":
                    if (session?.PlayerId is null)
                    {
                        AnsiConsole.MarkupLine("[yellow]Aucun joueur actif dans la session.[/]");
                        break;
                    }

                    await _dispatcher.DispatchAsync(new CommandContext
                    {
                        Group = "player",
                        Action = "show",
                        CommandId = "player.show",
                        Arguments = [session.PlayerId],
                        Options = new Dictionary<string, string?> { ["output"] = "text" },
                        PlayerId = session.PlayerId,
                        PlayerName = session.PlayerName,
                        Role = session.Role,
                        GameLevel = session.GameLevel
                    }, cancellationToken);
                    break;

                case "Annuaire joueurs":
                    await _dispatcher.DispatchAsync(new CommandContext
                    {
                        Group = "player",
                        Action = "list",
                        CommandId = "player.list",
                        Options = new Dictionary<string, string?> { ["output"] = "text" },
                        Role = session?.Role ?? Role.Player,
                        GameLevel = session?.GameLevel
                    }, cancellationToken);
                    break;

                case "Mon rating":
                    if (session?.PlayerId is null)
                    {
                        AnsiConsole.MarkupLine("[yellow]Aucun joueur actif dans la session.[/]");
                        break;
                    }

                    await _dispatcher.DispatchAsync(new CommandContext
                    {
                        Group = "rating",
                        Action = "show",
                        CommandId = "rating.show",
                        Arguments = [session.PlayerId],
                        Options = new Dictionary<string, string?> { ["output"] = "text" },
                        PlayerId = session.PlayerId,
                        PlayerName = session.PlayerName,
                        Role = session.Role,
                        GameLevel = session.GameLevel
                    }, cancellationToken);
                    break;

                case "Mes stats (30 jours)":
                    if (session?.PlayerId is null)
                    {
                        AnsiConsole.MarkupLine("[yellow]Aucun joueur actif dans la session.[/]");
                        break;
                    }

                    await _dispatcher.DispatchAsync(new CommandContext
                    {
                        Group = "rating",
                        Action = "stats",
                        CommandId = "rating.stats",
                        Arguments = [session.PlayerId],
                        Options = new Dictionary<string, string?>
                        {
                            ["30d"] = null,
                            ["output"] = "text"
                        },
                        PlayerId = session.PlayerId,
                        PlayerName = session.PlayerName,
                        Role = session.Role,
                        GameLevel = session.GameLevel
                    }, cancellationToken);
                    break;

                case "Top 10 mondial":
                    await _dispatcher.DispatchAsync(new CommandContext
                    {
                        Group = "rating",
                        Action = "leaderboard",
                        CommandId = "rating.leaderboard",
                        Options = new Dictionary<string, string?>
                        {
                            ["top"] = "10",
                            ["output"] = "text"
                        },
                        Role = session?.Role ?? Role.Player,
                        GameLevel = session?.GameLevel
                    }, cancellationToken);
                    break;
            }

            AnsiConsole.WriteLine();
            session = _sessionService.LoadSession();
        }

        return ExitCodes.Success;
    }

    private async Task<int> HandleDashboard(CancellationToken cancellationToken)
    {
        var session = _sessionService.LoadSession();
        if (session?.GameId is null || session.PlayerId is null)
        {
            AnsiConsole.MarkupLine("[yellow]Aucune partie active.[/]");
            return ExitCodes.GameNotFound;
        }

        await RenderTurnDashboard(session, cancellationToken);
        return ExitCodes.Success;
    }

    private int HandleClearSession()
    {
        var confirm = AnsiConsole.Confirm("[yellow]Supprimer la session locale ?[/]", defaultValue: false);
        if (!confirm)
            return ExitCodes.Success;

        _sessionService.ClearSession();
        AnsiConsole.MarkupLine("[green]Session locale effacee.[/]");
        return ExitCodes.Success;
    }

    private static CommandContext BuildMoveContext(SessionContext session)
    {
        var position = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Position de depart (ex: H8) :[/]")
                .Validate(value => !string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("La position est requise.")));

        var word = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Mot (minuscule = joker force) :[/]")
                .Validate(value => !string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Le mot est requis.")));

        var direction = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Direction[/]")
                .AddChoices("H", "V"));

        return BuildSessionBoundContext("play", "move", "play.move", session, [position, word, direction]);
    }

    private static CommandContext BuildSwapContext(SessionContext session)
    {
        var swapAll = AnsiConsole.Confirm("[green]Echanger tout le rack ?[/]", defaultValue: false);
        if (swapAll)
        {
            return BuildSessionBoundContext(
                "play", "swap", "play.swap", session,
                options: new Dictionary<string, string?> { ["all"] = null });
        }

        var letters = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Lettres a echanger (ex: AEI) :[/]")
                .Validate(value => !string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Les lettres sont requises.")));

        return BuildSessionBoundContext("play", "swap", "play.swap", session, [letters]);
    }

    private static CommandContext BuildCheckContext(SessionContext session)
    {
        var position = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Position de depart (ex: H8) :[/]")
                .Validate(value => !string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("La position est requise.")));

        var word = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Mot a verifier :[/]")
                .Validate(value => !string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Le mot est requis.")));

        var direction = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Direction[/]")
                .AddChoices("H", "V"));

        return BuildSessionBoundContext("play", "check", "play.check", session, [position, word, direction]);
    }

    private async Task RenderTurnDashboard(SessionContext session, CancellationToken cancellationToken)
    {
        var contexts = new[]
        {
            BuildSessionBoundContext("show", "board", "show.board", session),
            BuildSessionBoundContext("show", "rack", "show.rack", session),
            BuildSessionBoundContext("show", "scores", "show.scores", session)
        };

        foreach (var context in contexts)
        {
            var code = await _dispatcher.DispatchAsync(context, cancellationToken);
            if (code != ExitCodes.Success)
                _logger.LogDebug("Dashboard interactif: {CommandId} => {ExitCode}", context.CommandId, code);
        }
    }

    private static CommandContext BuildSessionBoundContext(
        string group,
        string action,
        string commandId,
        SessionContext session,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string?>? options = null)
    {
        return new CommandContext
        {
            Group = group,
            Action = action,
            CommandId = commandId,
            Arguments = arguments ?? [],
            Options = options ?? new Dictionary<string, string?>(),
            GameId = session.GameId,
            PlayerId = session.PlayerId,
            PlayerName = session.PlayerName,
            Role = session.Role,
            GameLevel = session.GameLevel
        };
    }
}
