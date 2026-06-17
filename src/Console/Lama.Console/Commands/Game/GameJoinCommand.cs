using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game join &lt;nom&gt;</c> — ajoute un joueur à la partie courante
/// et met à jour la session avec l'identité du joueur.
///
/// Arguments : nom du joueur (positionnel, requis).
/// Accessible à tous les rôles.
/// Met à jour la session persistée (PlayerId, PlayerName, Role → Player).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage JoinGame) — non encore implémenté.
/// La gestion de session est en place et sera activée dès que Lama.Core sera disponible.
/// </remarks>
public sealed class GameJoinCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.join";

    private readonly ISessionService _sessionService;
    private readonly ILogger<GameJoinCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameJoinCommand(ISessionService sessionService, ILogger<GameJoinCommand> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            global::System.Console.Error.WriteLine("[game join] Argument requis : <nom du joueur>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // Vérifier qu'une partie a été créée (session existante avec GameId)
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine(
                "[game join] Aucune partie active. " +
                "Créez d'abord une partie avec : lama game create");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        // TODO: appeler le cas d'usage JoinGame de Lama.Core pour :
        //   - vérifier que la partie accepte encore des joueurs
        //   - créer le rack du joueur
        //   - retourner un PlayerId
        // Exemple (à décommenter) :
        // var result = await _gameUseCase.JoinAsync(new JoinGameRequest(
        //     GameId:     context.GameId,
        //     PlayerName: playerName
        // ));
        // var playerId = result.PlayerId;

        _logger.LogWarning("{CommandId} : Lama.Core absent — session non mise à jour", CommandId);
        global::System.Console.Error.WriteLine(
            $"[game join] Non implémenté — Lama.Core absent. (joueur : {playerName})");

        // ── Ce bloc sera activé quand Lama.Core sera implémenté ──────────────
        // var currentSession = _sessionService.LoadSession()!;
        // var updatedSession = currentSession with
        // {
        //     PlayerId:   playerId,
        //     PlayerName: playerName,
        //     Role:       Role.Player,
        //     UpdatedAt:  DateTimeOffset.UtcNow
        // };
        // _sessionService.SaveSession(updatedSession);
        // global::System.Console.WriteLine($"Joueur '{playerName}' a rejoint la partie {context.GameId}.");
        // return Task.FromResult(ExitCodes.Success);
        // ─────────────────────────────────────────────────────────────────────

        return Task.FromResult(ExitCodes.GeneralError);
    }
}
