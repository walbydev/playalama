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
    private readonly ILogger<PlayChallengeCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayChallengeCommand(
        ChallengeWordUseCase challengeWordUseCase,
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        ILogger<PlayChallengeCommand> logger)
    {
        _challengeWordUseCase = challengeWordUseCase;
        _createGameUseCase = createGameUseCase;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine(
                "[play challenge] Aucune session active. Créez/rejoignez une partie d'abord.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        try
        {
            var result = _challengeWordUseCase.ExecuteAsync(
                new ChallengeWordRequest(context.GameId, context.PlayerId)).GetAwaiter().GetResult();

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
            return Task.FromResult(ExitCodes.Success);
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[play challenge] Erreur : {ex.Message}");
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

