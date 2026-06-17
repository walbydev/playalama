using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show scores</c> — affiche le tableau des scores de la partie.
/// Accessible à tous les rôles (lecture seule).
/// Options : --output (text|json|csv).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.GetGameState) et de ScoreRenderer — non encore implémentés.
/// </remarks>
public sealed class ShowScoresCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.scores";

    private readonly ILogger<ShowScoresCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowScoresCommand(ILogger<ShowScoresCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: obtenir les scores via IGameEngine.GetGameState()
        //       puis déléguer l'affichage à ScoreRenderer
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core / ScoreRenderer absents)", CommandId);
        global::System.Console.Error.WriteLine("[show scores] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
