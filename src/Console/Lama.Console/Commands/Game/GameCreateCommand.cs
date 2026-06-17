using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game create</c> — crée une nouvelle partie.
/// Options : --size (taille du plateau, défaut 15), --players (nb de joueurs, défaut 2),
///           --time-limit (limite en secondes par tour), --max-turns,
///           --level (casual|standard|competitive|tournament).
/// Réservée aux administrateurs.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage CreateGame) — non encore implémenté.
/// </remarks>
public sealed class GameCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.create";

    private readonly ILogger<GameCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameCreateCommand(ILogger<GameCreateCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler le cas d'usage CreateGame de Lama.Core
        // quand Lama.Core sera implémenté.
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[game create] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
