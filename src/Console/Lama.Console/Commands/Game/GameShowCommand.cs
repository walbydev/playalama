using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game show</c> — affiche les informations de la partie courante.
/// Accessible à tous les rôles (lecture seule).
/// Options : --output (text|json|csv).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.GetGameState) — non encore implémenté.
/// </remarks>
public sealed class GameShowCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.show";

    private readonly ILogger<GameShowCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameShowCommand(ILogger<GameShowCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler IGameEngine.GetGameState() via Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[game show] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
