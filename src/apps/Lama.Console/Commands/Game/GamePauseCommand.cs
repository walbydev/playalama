using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game pause</c> — met en pause la partie courante.
/// Accessible aux admins et aux joueurs (pas aux spectateurs).
/// </summary>
public sealed class GamePauseCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.pause";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly ILogger<GamePauseCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GamePauseCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        ILogger<GamePauseCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[game pause] Aucune partie active a mettre en pause.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        try
        {
            var engine = _createGameUseCase.GetEngine(context.GameId);
            if (engine is null)
            {
                global::System.Console.Error.WriteLine(
                    $"[game pause] Partie introuvable : {context.GameId}");
                return Task.FromResult(ExitCodes.GameNotFound);
            }

            var state = engine.GetGameState();
            var gameLevel = context.GameLevel ?? GameLevel.Standard;
            var isFirstMove = state.TurnNumber == 1 && IsBoardEmpty(state.Board);

            _createGameUseCase.SaveGame(context.GameId, gameLevel, isFirstMove);

            var session = _sessionService.LoadSession();
            if (session is not null)
                _sessionService.SaveSession(session with { UpdatedAt = DateTimeOffset.UtcNow });

            global::System.Console.WriteLine($"✓ Partie mise en pause ({context.GameId})");
            global::System.Console.WriteLine("  Etat persisté sur disque. Vous pouvez quitter le terminal si besoin.");

            _logger.LogInformation("Partie mise en pause : {GameId}", context.GameId);
            return Task.FromResult(ExitCodes.Success);
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[game pause] Erreur : {ex.Message}");
            return Task.FromResult(ExitCodes.GeneralError);
        }
    }

    private static bool IsBoardEmpty(BoardState board)
    {
        for (var row = 0; row < 15; row++)
            for (var col = 0; col < 15; col++)
                if (board.Grid[row, col] is not null)
                    return false;

        return true;
    }
}

