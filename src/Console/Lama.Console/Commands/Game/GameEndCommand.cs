using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game end</c> — met fin à la partie courante.
/// Options : --with-scores (affiche le classement final),
///           --export &lt;fichier&gt; (exporte les résultats),
///           --force (terminaison forcée, admin uniquement → game.end.force).
/// Accessible aux joueurs pour une fin normale ; --force réservé aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.EndGame) — non encore implémenté.
/// </remarks>
public sealed class GameEndCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.end";

    private readonly ILogger<GameEndCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameEndCommand(ILogger<GameEndCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler IGameEngine.EndGame() via Lama.Core
        // Si --force → vérifier que le rôle est Admin (game.end.force)
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[game end] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
