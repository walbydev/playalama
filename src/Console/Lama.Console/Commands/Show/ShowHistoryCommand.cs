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
    private readonly ILogger<ShowHistoryCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowHistoryCommand(CreateGameUseCase createGameUseCase, ILogger<ShowHistoryCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[show history] Aucune partie active.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine($"[show history] Partie introuvable : {context.GameId}");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var history = engine.GetGameState().History;
        if (history.Count == 0)
        {
            global::System.Console.WriteLine("Aucun coup joué pour le moment.");
            return Task.FromResult(ExitCodes.Success);
        }

        var lastOption = context.GetOption("last");
        if (!string.IsNullOrWhiteSpace(lastOption) && (!int.TryParse(lastOption, out var lastCount) || lastCount <= 0))
        {
            global::System.Console.Error.WriteLine("[show history] Option --last invalide.");
            return Task.FromResult(ExitCodes.InvalidArgument);
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
        return Task.FromResult(ExitCodes.Success);
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
}
