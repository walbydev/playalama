using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show scores</c> — affiche le tableau des scores.
/// </summary>
public sealed class ShowScoresCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.scores";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ILogger<ShowScoresCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowScoresCommand(CreateGameUseCase createGameUseCase, ILogger<ShowScoresCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _logger            = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[show scores] Aucune partie active.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine(
                "[show scores] Partie introuvable en mémoire.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var state = engine.GetGameState();

        global::System.Console.WriteLine($"Tour {state.TurnNumber} — Scores :");
        global::System.Console.WriteLine(new string('─', 30));

        var sorted = state.Players
            .OrderByDescending(p => p.Score)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var player  = sorted[i];
            var isCurrent = state.Players.IndexOf(player) == state.CurrentPlayerIndex;
            var marker  = isCurrent ? "▶ " : "  ";
            global::System.Console.WriteLine(
                $"{marker}{i + 1}. {player.Name,-20} {player.Score,5} pts");
        }

        global::System.Console.WriteLine(new string('─', 30));

        if (context.OutputFormat == "json")
        {
            var json = "[" + string.Join(",",
                sorted.Select(p => $"{{\"name\":\"{p.Name}\",\"score\":{p.Score}}}")) + "]";
            global::System.Console.WriteLine(json);
        }

        return Task.FromResult(ExitCodes.Success);
    }
}
