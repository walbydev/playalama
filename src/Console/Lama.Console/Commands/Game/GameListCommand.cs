using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game list</c> — liste les parties disponibles.
/// Accessible à tous les rôles (lecture seule).
/// Options : --output (text|json|csv).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage ListGames) — non encore implémenté.
/// </remarks>
public sealed class GameListCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.list";

    private readonly ILogger<GameListCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameListCommand(ILogger<GameListCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler le cas d'usage ListGames de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[game list] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
