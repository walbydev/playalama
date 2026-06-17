using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show rack</c> — affiche le rack (7 lettres) du joueur courant.
/// Accessible aux joueurs et aux admins (pas aux spectateurs).
/// Options : --with-values (affiche la valeur en points de chaque lettre).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.GetCurrentPlayer) et de RackRenderer — non encore implémentés.
/// </remarks>
public sealed class ShowRackCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.rack";

    private readonly ILogger<ShowRackCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowRackCommand(ILogger<ShowRackCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: obtenir le rack via IGameEngine.GetCurrentPlayer()
        //       puis déléguer l'affichage à RackRenderer
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core / RackRenderer absents)", CommandId);
        global::System.Console.Error.WriteLine("[show rack] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
