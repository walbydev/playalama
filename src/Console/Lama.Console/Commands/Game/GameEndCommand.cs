using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game end [--with-scores]</c>
/// — termine la partie courante et efface la session.
/// </summary>
public sealed class GameEndCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.end";

    private readonly EndGameUseCase  _endGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly ILogger<GameEndCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameEndCommand(
        EndGameUseCase  endGameUseCase,
        ISessionService sessionService,
        ILogger<GameEndCommand> logger)
    {
        _endGameUseCase = endGameUseCase;
        _sessionService = sessionService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[game end] Aucune partie active à terminer.");
            return ExitCodes.GameNotFound;
        }

        try
        {
            var response = await _endGameUseCase.ExecuteAsync(
                new EndGameRequest(
                    GameId: context.GameId,
                    GameLevel: context.GameLevel));

            // Afficher le classement final
            global::System.Console.WriteLine("═══════════════════════════════════");
            global::System.Console.WriteLine("        PARTIE TERMINÉE");
            global::System.Console.WriteLine("═══════════════════════════════════");
            global::System.Console.WriteLine();

            if (response.Winner is not null)
                global::System.Console.WriteLine($"🏆 Gagnant : {response.Winner}");
            else
                global::System.Console.WriteLine("Égalité !");

            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Classement final :");
            var rank = 1;
            foreach (var (name, score) in response.Scores)
            {
                global::System.Console.WriteLine($"  {rank++}. {name,-20} {score,5} pts");
            }
            global::System.Console.WriteLine();

            // Effacer la session
            _sessionService.ClearSession();
            global::System.Console.WriteLine("Session effacée.");

            _logger.LogInformation("Partie terminée : {GameId}", context.GameId);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[game end] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
