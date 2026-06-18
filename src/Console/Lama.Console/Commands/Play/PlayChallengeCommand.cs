using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play challenge</c> — conteste le dernier mot joué par l'adversaire.
/// Si le mot contesté est invalide, l'adversaire retire ses lettres et perd son tour.
/// Si le mot est valide, le challengeur perd son propre tour.
/// Accessible aux joueurs et aux admins.
/// </summary>
public sealed class PlayChallengeCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.challenge";

    private readonly ChallengeWordUseCase _challengeWordUseCase;
    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<PlayChallengeCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayChallengeCommand(
        ChallengeWordUseCase challengeWordUseCase,
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        ILogger<PlayChallengeCommand> logger)
        : this(
            challengeWordUseCase,
            createGameUseCase,
            sessionService,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public PlayChallengeCommand(
        ChallengeWordUseCase challengeWordUseCase,
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<PlayChallengeCommand> logger)
    {
        _challengeWordUseCase = challengeWordUseCase;
        _createGameUseCase = createGameUseCase;
        _sessionService = sessionService;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine(
                "[play challenge] Aucune session active. Créez/rejoignez une partie d'abord.");
            return ExitCodes.GameNotFound;
        }

        try
        {
            if (_runtimeMode.IsOnline)
            {
                var response = await _onlineGameGateway.PlayCommandAsync(
                    context.GameId,
                    context.PlayerId,
                    "play.challenge",
                    payload: null,
                    cancellationToken);

                var snapshot = await _onlineGameGateway.GetGameAsync(context.GameId, cancellationToken);
                var nextPlayerName = snapshot.CurrentPlayerIndex >= 0 && snapshot.CurrentPlayerIndex < snapshot.Players.Count
                    ? snapshot.Players[snapshot.CurrentPlayerIndex].PlayerName
                    : response.NextPlayerId ?? "inconnu";

                global::System.Console.WriteLine(response.Message ?? "Challenge traité (online).");
                global::System.Console.WriteLine($"  Tour suivant : {nextPlayerName}");

                _logger.LogInformation("{CommandId} online : challenge traite", CommandId);
                return ExitCodes.Success;
            }

            var result = await _challengeWordUseCase.ExecuteAsync(
                new ChallengeWordRequest(context.GameId, context.PlayerId));

            var session = _sessionService.LoadSession();
            var gameLevel = session?.GameLevel ?? GameLevel.Standard;
            var isFirstMove = result.GameState.TurnNumber == 1 && IsBoardEmpty(result.GameState.Board);
            _createGameUseCase.SaveGame(context.GameId, gameLevel, isFirstMove);

            if (session is not null)
                _sessionService.SaveSession(session with { UpdatedAt = DateTimeOffset.UtcNow });

            global::System.Console.WriteLine(result.Message);
            global::System.Console.WriteLine(
                $"  Tour suivant : {result.GameState.Players[result.GameState.CurrentPlayerIndex].Name}");

            _logger.LogInformation("{CommandId} : challenge traité", CommandId);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[play challenge] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
        catch (HttpRequestException ex)
        {
            global::System.Console.Error.WriteLine($"[play challenge] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
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

