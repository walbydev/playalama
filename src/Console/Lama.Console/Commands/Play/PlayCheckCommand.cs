using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play check &lt;case&gt; &lt;mot&gt; &lt;direction&gt;</c>
/// — vérifie la validité d'un coup sans le jouer (aide pour débutants).
/// Accessible uniquement en mode Casual (et aux admins).
/// Arguments : identiques à <c>play move</c>.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.ValidateMove) — non encore implémenté.
/// </remarks>
public sealed class PlayCheckCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.check";

    private readonly ILogger<PlayCheckCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayCheckCommand(ILogger<PlayCheckCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var position  = context.GetArgument(0);
        var word      = context.GetArgument(1);
        var direction = context.GetArgument(2);

        if (string.IsNullOrWhiteSpace(position) ||
            string.IsNullOrWhiteSpace(word) ||
            string.IsNullOrWhiteSpace(direction))
        {
            global::System.Console.Error.WriteLine(
                "[play check] Arguments requis : <case> <mot> <direction>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // TODO: appeler IGameEngine.ValidateMove() via Lama.Core (sans consommer le tour)
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[play check] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
