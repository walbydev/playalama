using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show scores</c> — affiche le tableau des scores.
/// </summary>
public sealed class ShowScoresCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.scores";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<ShowScoresCommand> _logger;

    /// <summary>
    /// Constructeur de rétrocompatibilité (mode local uniquement).
    /// </summary>
    public ShowScoresCommand(CreateGameUseCase createGameUseCase, ILogger<ShowScoresCommand> logger)
        : this(
            createGameUseCase,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public ShowScoresCommand(
        CreateGameUseCase createGameUseCase,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<ShowScoresCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[show scores] Aucune partie active.");
            return ExitCodes.GameNotFound;
        }

        if (_runtimeMode.IsOnline)
        {
            try
            {
                var snapshot = await _onlineGameGateway.GetGameAsync(context.GameId, cancellationToken);

                var sortedPlayers = snapshot.Players
                    .OrderByDescending(p => p.Score)
                    .ToList();

                if (context.OutputFormat == "json")
                {
                    global::System.Console.WriteLine(JsonSerializer.Serialize(sortedPlayers.Select(p => new
                    {
                        name = p.PlayerName,
                        score = p.Score,
                        isCurrent = snapshot.Players.FindIndex(sp => sp.PlayerId == p.PlayerId) == snapshot.CurrentPlayerIndex
                    })));
                    return ExitCodes.Success;
                }

                global::System.Console.WriteLine($"Tour {snapshot.TurnNumber} — Scores :");
                global::System.Console.WriteLine(new string('─', 42));

                for (var i = 0; i < sortedPlayers.Count; i++)
                {
                    var player = sortedPlayers[i];
                    var marker = snapshot.Players.FindIndex(p => p.PlayerId == player.PlayerId) == snapshot.CurrentPlayerIndex ? "▶ " : "  ";
                    global::System.Console.WriteLine($"{marker}{i + 1}. {player.PlayerName,-20} {player.Score,5} pts");
                }

                global::System.Console.WriteLine(new string('─', 42));


                return ExitCodes.Success;
            }
            catch (HttpRequestException ex)
            {
                global::System.Console.Error.WriteLine($"[show scores] Erreur online : {ex.Message}");
                return ExitCodes.GeneralError;
            }
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine(
                "[show scores] Partie introuvable en mémoire.");
            return ExitCodes.GameNotFound;
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

        return ExitCodes.Success;
    }
}
