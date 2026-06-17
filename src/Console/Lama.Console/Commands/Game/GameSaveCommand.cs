using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game save</c> — sauvegarde l'état de la partie courante.
/// Accessible aux admins et aux joueurs (pas aux spectateurs).
/// Options : --file (chemin de sauvegarde optionnel).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Infrastructure (persistance) — non encore implémenté.
/// </remarks>
public sealed class GameSaveCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.save";

    private readonly ILogger<GameSaveCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameSaveCommand(ILogger<GameSaveCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler le service de persistance de Lama.Infrastructure
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Infrastructure absent)", CommandId);
        global::System.Console.Error.WriteLine("[game save] Non implémenté — Lama.Infrastructure absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
