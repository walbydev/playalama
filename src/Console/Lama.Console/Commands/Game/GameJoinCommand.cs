using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game join &lt;nom&gt;</c>
/// — ajoute un joueur à la partie courante et met à jour la session.
/// </summary>
public sealed class GameJoinCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.join";

    private readonly JoinGameUseCase  _joinGameUseCase;
    private readonly ISessionService  _sessionService;
    private readonly ILogger<GameJoinCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameJoinCommand(
        JoinGameUseCase  joinGameUseCase,
        ISessionService  sessionService,
        ILogger<GameJoinCommand> logger)
    {
        _joinGameUseCase = joinGameUseCase;
        _sessionService  = sessionService;
        _logger          = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var playerName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            global::System.Console.Error.WriteLine("[game join] Argument requis : <nom du joueur>");
            return ExitCodes.InvalidArgument;
        }

        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine(
                "[game join] Aucune partie active. Créez d'abord une partie : lama game create");
            return ExitCodes.GameNotFound;
        }

        try
        {
            var response = await _joinGameUseCase.ExecuteAsync(
                new JoinGameRequest(context.GameId, playerName));

            // Mettre à jour la session avec le nouvel ID de joueur
            var current  = _sessionService.LoadSession()!;
            var updated = current with
            {
                PlayerId   = response.PlayerId,
                PlayerName = playerName,
                Role       = Role.Player,
                UpdatedAt  = DateTimeOffset.UtcNow
            };
            _sessionService.SaveSession(updated);

            global::System.Console.WriteLine($"✓ {playerName} a rejoint la partie.");
            global::System.Console.WriteLine($"  Rack initial : {string.Join(" ", response.Rack)}");
            global::System.Console.WriteLine($"  Joueurs : {response.GameState.Players.Count}");

            _logger.LogInformation("{Player} a rejoint {GameId}", playerName, context.GameId);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[game join] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
