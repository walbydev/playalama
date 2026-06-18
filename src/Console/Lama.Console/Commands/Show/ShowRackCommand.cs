using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show rack [--with-values]</c>
/// — affiche le rack du joueur courant.
/// </summary>
public sealed class ShowRackCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.rack";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly IGameLanguageProvider _languageProvider;
    private readonly ILogger<ShowRackCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowRackCommand(
        CreateGameUseCase  createGameUseCase,
        IGameLanguageProvider languageProvider,
        ILogger<ShowRackCommand> logger)
        : this(
            createGameUseCase,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            languageProvider,
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public ShowRackCommand(
        CreateGameUseCase  createGameUseCase,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        IGameLanguageProvider languageProvider,
        ILogger<ShowRackCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _runtimeMode       = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _languageProvider  = languageProvider;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine("[show rack] Aucune session active.");
            return ExitCodes.GameNotFound;
        }

        if (_runtimeMode.IsOnline)
        {
            try
            {
                var snapshot = await _onlineGameGateway.GetGameAsync(context.GameId, cancellationToken);
                var onlinePlayer = context.PlayerId is not null
                    ? snapshot.Players.FirstOrDefault(p => p.PlayerId == context.PlayerId)
                    : null;

                onlinePlayer ??= context.PlayerName is not null
                    ? snapshot.Players.FirstOrDefault(p => p.PlayerName == context.PlayerName)
                    : snapshot.Players.ElementAtOrDefault(snapshot.CurrentPlayerIndex);

                if (onlinePlayer is null)
                {
                    global::System.Console.Error.WriteLine("[show rack] Joueur introuvable.");
                    return ExitCodes.GeneralError;
                }

                PrintRack(onlinePlayer.PlayerName, onlinePlayer.Rack, context.HasOption("with-values"));
                return ExitCodes.Success;
            }
            catch (HttpRequestException ex)
            {
                global::System.Console.Error.WriteLine($"[show rack] Erreur online : {ex.Message}");
                return ExitCodes.GeneralError;
            }
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine(
                "[show rack] Partie introuvable en mémoire.");
            return ExitCodes.GameNotFound;
        }

        var state       = engine.GetGameState();

        // Trouver le joueur par son nom (stocké dans la session)
        var playerName  = context.PlayerName ?? context.GetOption("player");
        var player      = playerName is not null
            ? state.Players.FirstOrDefault(p => p.Name == playerName)
            : state.Players.ElementAtOrDefault(state.CurrentPlayerIndex);

        if (player is null)
        {
            global::System.Console.Error.WriteLine("[show rack] Joueur introuvable.");
            return ExitCodes.GeneralError;
        }

        PrintRack(player.Name, player.Rack, context.HasOption("with-values"));

        return ExitCodes.Success;
    }

    private void PrintRack(string playerName, IReadOnlyList<char> rack, bool withValues)
    {
        var scores = _languageProvider.GetLetterScores();

        global::System.Console.WriteLine($"Rack de {playerName} :");
        global::System.Console.Write("  ");

        foreach (var letter in rack)
        {
            if (withValues && scores.TryGetValue(letter, out var pts))
                global::System.Console.Write($"[{letter}({pts})] ");
            else
                global::System.Console.Write($"[{letter}] ");
        }

        global::System.Console.WriteLine();
        global::System.Console.WriteLine($"  {rack.Count} lettre(s)");
    }
}
