using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game pause</c> — met en pause la partie courante.
/// Accessible aux admins et aux joueurs (pas aux spectateurs).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage PauseGame) — non encore implémenté.
/// </remarks>
public sealed class GamePauseCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.pause";

    private readonly ILogger<GamePauseCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GamePauseCommand(ILogger<GamePauseCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler le cas d'usage PauseGame de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[game pause] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
