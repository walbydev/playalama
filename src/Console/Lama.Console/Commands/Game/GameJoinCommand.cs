using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game join &lt;nom&gt;</c> — ajoute un joueur à la partie courante.
/// Arguments : nom du joueur (positionnel, requis).
/// Accessible à tous les rôles.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage JoinGame) — non encore implémenté.
/// </remarks>
public sealed class GameJoinCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.join";

    private readonly ILogger<GameJoinCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameJoinCommand(ILogger<GameJoinCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            global::System.Console.Error.WriteLine("[game join] Argument requis : <nom du joueur>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // TODO: appeler le cas d'usage JoinGame de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine($"[game join] Non implémenté — Lama.Core absent. (joueur : {playerName})");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
