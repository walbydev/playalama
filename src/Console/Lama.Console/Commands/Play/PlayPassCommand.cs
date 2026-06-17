using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play pass</c> — passe le tour du joueur courant.
/// </summary>
public sealed class PlayPassCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.pass";

    private readonly PassTurnUseCase _passTurnUseCase;
    private readonly ILogger<PlayPassCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayPassCommand(PassTurnUseCase passTurnUseCase, ILogger<PlayPassCommand> logger)
    {
        _passTurnUseCase = passTurnUseCase;
        _logger          = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine(
                "[play pass] Aucune session active. Créez/rejoignez une partie d'abord.");
            return ExitCodes.GameNotFound;
        }

        try
        {
            var newState = await _passTurnUseCase.ExecuteAsync(
                new PassTurnRequest(context.GameId, context.PlayerId));

            global::System.Console.WriteLine(
                $"✓ Tour passé. C'est maintenant au tour de : " +
                $"{newState.Players[newState.CurrentPlayerIndex].Name}");

            _logger.LogInformation("{Player} a passé son tour", context.PlayerName);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[play pass] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
