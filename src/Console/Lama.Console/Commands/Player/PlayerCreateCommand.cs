using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Player;

/// <summary>
/// Commande <c>lama player create &lt;nom&gt;</c> — crée un profil joueur.
/// Arguments : nom du joueur (positionnel, requis).
/// Accessible aux joueurs et aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage CreatePlayer) — non encore implémenté.
/// </remarks>
public sealed class PlayerCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "player.create";

    private readonly ILogger<PlayerCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayerCreateCommand(ILogger<PlayerCreateCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            global::System.Console.Error.WriteLine("[player create] Argument requis : <nom>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // TODO: appeler le cas d'usage CreatePlayer de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine(
            $"[player create] Non implémenté — Lama.Core absent. (nom : {playerName})");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
