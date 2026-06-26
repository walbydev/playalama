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
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<GameEndCommand> _logger;

    /// <summary>
    /// Constructeur de rétrocompatibilité (mode local uniquement).
    /// </summary>
    public GameEndCommand(
        EndGameUseCase endGameUseCase,
        ISessionService sessionService,
        ILogger<GameEndCommand> logger)
        : this(
            endGameUseCase,
            sessionService,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public GameEndCommand(
        EndGameUseCase  endGameUseCase,
        ISessionService sessionService,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<GameEndCommand> logger)
    {
        _endGameUseCase = endGameUseCase;
        _sessionService = sessionService;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
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
            string? winner;
            IReadOnlyList<(string PlayerName, int Score)> scores;

            if (_runtimeMode.IsOnline)
            {
                // Restaure le token sauvegardé en session (comme GameCreateCommand / GameJoinCommand)
                var existingSession = _sessionService.LoadSession();
                _onlineGameGateway.SetAuthToken(existingSession?.AuthToken);

                await _onlineGameGateway.EnsureAuthenticatedAsync(
                    context.PlayerName ?? "Joueur",
                    context.PlayerId,
                    cancellationToken);

                var onlineResponse = await _onlineGameGateway.EndGameAsync(
                    context.GameId,
                    context.PlayerId,
                    cancellationToken);

                winner = onlineResponse.Winner;
                scores = onlineResponse.Scores
                    .Select(s => (s.PlayerName, s.Score))
                    .ToList()
                    .AsReadOnly();
            }
            else
            {
                var response = await _endGameUseCase.ExecuteAsync(
                    new EndGameRequest(
                        GameId: context.GameId,
                        GameLevel: context.GameLevel));

                winner = response.Winner;
                scores = response.Scores;
            }

            // Afficher le classement final
            global::System.Console.WriteLine("═══════════════════════════════════");
            global::System.Console.WriteLine("        PARTIE TERMINÉE");
            global::System.Console.WriteLine("═══════════════════════════════════");
            global::System.Console.WriteLine();

            if (winner is not null)
                global::System.Console.WriteLine($"🏆 Gagnant : {winner}");
            else
                global::System.Console.WriteLine("Égalité !");

            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Classement final :");
            var rank = 1;
            foreach (var (name, score) in scores)
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
        catch (HttpRequestException ex)
        {
            global::System.Console.Error.WriteLine($"[game end] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
