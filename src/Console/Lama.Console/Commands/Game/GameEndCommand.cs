using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game end</c> — termine la partie courante et efface la session.
///
/// Options :
/// <list type="bullet">
///   <item><c>--with-scores</c>      Affiche le classement final</item>
///   <item><c>--export &lt;fichier&gt;</c> Exporte les résultats au format JSON</item>
///   <item><c>--force</c>             Terminaison forcée (réservé aux admins → game.end.force)</item>
/// </list>
///
/// Accessible aux joueurs pour une fin normale.
/// <c>--force</c> est réservé aux admins et passe par <c>game.end.force</c>.
/// Efface le fichier de session local après la fin de partie.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.EndGame) — non encore implémenté.
/// La gestion de session (ClearSession) est en place et sera activée dès que Lama.Core sera disponible.
/// </remarks>
public sealed class GameEndCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.end";

    private readonly ISessionService _sessionService;
    private readonly ILogger<GameEndCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameEndCommand(ISessionService sessionService, ILogger<GameEndCommand> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession)
        {
            global::System.Console.Error.WriteLine(
                "[game end] Aucune partie active à terminer.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        // TODO: appeler IGameEngine.EndGame() via Lama.Core pour :
        //   - calculer les scores finaux
        //   - valider la fin de partie
        //   - générer le rapport si --export
        // Si --force → la commande doit être enregistrée sous "game.end.force"
        //   (AccessControlService la réservera aux admins)

        _logger.LogWarning("{CommandId} : Lama.Core absent — session non effacée", CommandId);
        global::System.Console.Error.WriteLine(
            "[game end] Non implémenté — Lama.Core absent. La session n'est pas effacée.");

        // ── Ce bloc sera activé quand Lama.Core sera implémenté ──────────────
        // await _gameUseCase.EndAsync(context.GameId!);
        //
        // if (context.HasOption("with-scores"))
        // {
        //     // Afficher le classement via ScoreRenderer
        // }
        //
        // if (context.GetOption("export") is { } exportPath)
        // {
        //     // Exporter les résultats via Lama.Infrastructure
        // }
        //
        // // Effacer la session — la partie est terminée
        // _sessionService.ClearSession();
        // global::System.Console.WriteLine("Partie terminée. Session effacée.");
        // return Task.FromResult(ExitCodes.Success);
        // ─────────────────────────────────────────────────────────────────────

        return Task.FromResult(ExitCodes.GeneralError);
    }
}
