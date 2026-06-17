using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play pass</c> — passe le tour du joueur courant.
/// Accessible aux joueurs et aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.PassTurn) — non encore implémenté.
/// </remarks>
public sealed class PlayPassCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.pass";

    private readonly ILogger<PlayPassCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayPassCommand(ILogger<PlayPassCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler IGameEngine.PassTurn() via Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[play pass] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
