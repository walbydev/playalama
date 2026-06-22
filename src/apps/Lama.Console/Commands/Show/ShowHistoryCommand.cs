using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show history</c> — affiche l'historique des coups joués.
/// Accessible à tous les rôles (lecture seule).
/// Options : --last N (affiche les N derniers coups uniquement),
///           --output (text|json|csv).
/// </summary>
public sealed class ShowHistoryCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.history";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<ShowHistoryCommand> _logger;

    /// <summary>
    /// Constructeur de rétrocompatibilité (mode local uniquement).
    /// </summary>
    public ShowHistoryCommand(CreateGameUseCase createGameUseCase, ILogger<ShowHistoryCommand> logger)
        : this(
            createGameUseCase,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public ShowHistoryCommand(
        CreateGameUseCase createGameUseCase,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<ShowHistoryCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[show history] Aucune partie active.");
            return ExitCodes.GameNotFound;
        }

        if (_runtimeMode.IsOnline)
        {
            try
            {
                var snapshot = await _onlineGameGateway.GetGameAsync(context.GameId, cancellationToken);
                var moves = snapshot.Moves;

                if (moves.Count == 0)
                {
                    global::System.Console.WriteLine("Aucun coup joué pour le moment.");
                    return ExitCodes.Success;
                }

                var onlineLastOption = context.GetOption("last");
                if (!string.IsNullOrWhiteSpace(onlineLastOption) && (!int.TryParse(onlineLastOption, out var onlineLastCount) || onlineLastCount <= 0))
                {
                    global::System.Console.Error.WriteLine("[show history] Option --last invalide.");
                    return ExitCodes.InvalidArgument;
                }

                var onlineItems = moves
                    .TakeLast(int.TryParse(onlineLastOption, out var onlineN) && onlineN > 0 ? onlineN : moves.Count)
                    .ToList();

                var displayItems = onlineItems
                    .Select((item, index) => ToDisplayItem(item, index + 1))
                    .ToList();

                switch (context.OutputFormat.ToLowerInvariant())
                {
                    case "json":
                        global::System.Console.WriteLine(JsonSerializer.Serialize(displayItems));
                        break;

                    case "csv":
                        global::System.Console.WriteLine("turn,player,command,placements,score,playedAt");
                        foreach (var item in displayItems)
                            global::System.Console.WriteLine($"{item.TurnNumber},{item.PlayerName},{item.Command},\"{item.Placements}\",{item.Score},{item.PlayedAt:O}");
                        break;

                    default:
                        global::System.Console.WriteLine("Historique des coups :");
                        foreach (var item in displayItems)
                        {
                            var details = string.IsNullOrWhiteSpace(item.Placements)
                                ? item.Command
                                : item.Placements;
                            global::System.Console.WriteLine(
                                $"- Tour {item.TurnNumber} | {item.PlayerName} | {details} | {item.Score} pts");
                        }
                        break;
                }

                _logger.LogInformation("{CommandId} online : {Count} entrée(s) affichée(s)", CommandId, onlineItems.Count);
                return ExitCodes.Success;
            }
            catch (HttpRequestException ex)
            {
                global::System.Console.Error.WriteLine($"[show history] Erreur online : {ex.Message}");
                return ExitCodes.GeneralError;
            }
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine($"[show history] Partie introuvable : {context.GameId}");
            return ExitCodes.GameNotFound;
        }

        var history = engine.GetGameState().History;
        if (history.Count == 0)
        {
            global::System.Console.WriteLine("Aucun coup joué pour le moment.");
            return ExitCodes.Success;
        }

        var lastOption = context.GetOption("last");
        if (!string.IsNullOrWhiteSpace(lastOption) && (!int.TryParse(lastOption, out var lastCount) || lastCount <= 0))
        {
            global::System.Console.Error.WriteLine("[show history] Option --last invalide.");
            return ExitCodes.InvalidArgument;
        }

        var items = history
            .TakeLast(int.TryParse(lastOption, out var n) && n > 0 ? n : history.Count)
            .ToList();

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(items.Select(ToDisplayItem)));
                break;

            case "csv":
                global::System.Console.WriteLine("turn,player,placements,score,playedAt");
                foreach (var item in items)
                {
                    global::System.Console.WriteLine(
                        $"{item.TurnNumber},{item.PlayerName},\"{item.Placements}\",{item.Score},{item.PlayedAt:O}");
                }
                break;

            default:
                global::System.Console.WriteLine("Historique des coups :");
                foreach (var item in items)
                {
                    global::System.Console.WriteLine(
                        $"- Tour {item.TurnNumber} | {item.PlayerName} | {item.Placements} | {item.Score} pts");
                }
                break;
        }

        _logger.LogInformation("{CommandId} : {Count} entrée(s) affichée(s)", CommandId, items.Count);
        return ExitCodes.Success;
    }

    private static object ToDisplayItem(GameMove move) => new
    {
        move.TurnNumber,
        move.PlayerName,
        Placements = string.Join(
            ", ",
            move.Placements.Select(p => $"{(char)('A' + p.Column)}{p.Row + 1}:{p.Letter}")),
        move.Score,
        move.PlayedAt
    };

    private static OnlineHistoryDisplayItem ToDisplayItem(OnlineSnapshotMove move, int fallbackTurn)
    {
        var placements = move.Placements is { Count: > 0 }
            ? string.Join(
                ", ",
                move.Placements
                    .OrderBy(p => p.Row)
                    .ThenBy(p => p.Column)
                    .Select(p => $"{(char)('A' + p.Column)}{p.Row + 1}:{p.Letter}"))
            : string.Empty;

        return new OnlineHistoryDisplayItem(
            MoveId: move.MoveId,
            PlayerId: move.PlayerId,
            TurnNumber: move.TurnNumber > 0 ? move.TurnNumber : fallbackTurn,
            PlayerName: move.PlayerName,
            Command: move.Command,
            Placements: placements,
            Score: move.Score,
            PlayedAt: move.PlayedAt);
    }

    private sealed record OnlineHistoryDisplayItem(
        string MoveId,
        string PlayerId,
        int TurnNumber,
        string PlayerName,
        string Command,
        string Placements,
        int Score,
        DateTimeOffset PlayedAt);
}
