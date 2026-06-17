using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show board</c> — affiche le plateau de jeu courant.
/// Accessible à tous les rôles (lecture seule).
/// Options : --last-move (met en évidence le dernier coup joué),
///           --no-color (désactive les couleurs ANSI),
///           --high-contrast (mode contraste élevé).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.GetGameState) et de BoardRenderer — non encore implémentés.
/// </remarks>
public sealed class ShowBoardCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.board";

    private readonly ILogger<ShowBoardCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowBoardCommand(ILogger<ShowBoardCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: obtenir l'état du plateau via IGameEngine.GetGameState()
        //       puis déléguer l'affichage à BoardRenderer
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core / BoardRenderer absents)", CommandId);
        global::System.Console.Error.WriteLine("[show board] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
