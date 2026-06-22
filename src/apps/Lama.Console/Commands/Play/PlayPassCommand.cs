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
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<PlayPassCommand> _logger;

    /// <summary>
    /// Constructeur de rétrocompatibilité (mode local uniquement).
    /// </summary>
    public PlayPassCommand(PassTurnUseCase passTurnUseCase, ILogger<PlayPassCommand> logger)
        : this(
            passTurnUseCase,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public PlayPassCommand(
        PassTurnUseCase passTurnUseCase,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<PlayPassCommand> logger)
    {
        _passTurnUseCase = passTurnUseCase;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
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
            if (_runtimeMode.IsOnline)
            {
                await _onlineGameGateway.EnsureAuthenticatedAsync(
                    context.PlayerName ?? "Joueur",
                    context.PlayerId,
                    cancellationToken);

                var response = await _onlineGameGateway.PlayCommandAsync(
                    context.GameId,
                    context.PlayerId,
                    "play.pass",
                    payload: null,
                    cancellationToken);

                var snapshot = await _onlineGameGateway.GetGameAsync(context.GameId, cancellationToken);
                var nextPlayerName = snapshot.CurrentPlayerIndex >= 0 && snapshot.CurrentPlayerIndex < snapshot.Players.Count
                    ? snapshot.Players[snapshot.CurrentPlayerIndex].PlayerName
                    : response.NextPlayerId ?? "inconnu";

                global::System.Console.WriteLine(
                    $"✓ Tour passé (online). C'est maintenant au tour de : {nextPlayerName}");

                _logger.LogInformation("{Player} a passé son tour (online)", context.PlayerName);
                return ExitCodes.Success;
            }

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
        catch (HttpRequestException ex)
        {
            global::System.Console.Error.WriteLine($"[play pass] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
