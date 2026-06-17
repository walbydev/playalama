using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show history</c> — affiche l'historique des coups joués.
/// Accessible à tous les rôles (lecture seule).
/// Options : --last N (affiche les N derniers coups uniquement),
///           --output (text|json|csv).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (historique des coups dans GameState) — non encore implémenté.
/// </remarks>
public sealed class ShowHistoryCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.history";

    private readonly ILogger<ShowHistoryCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowHistoryCommand(ILogger<ShowHistoryCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: obtenir l'historique via Lama.Core
        //       Option --last : context.GetOption("last") → int.TryParse
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[show history] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
