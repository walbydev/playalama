using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play challenge</c> — conteste le dernier mot joué par l'adversaire.
/// Si le mot contesté est invalide, l'adversaire retire ses lettres et perd son tour.
/// Si le mot est valide, le challengeur perd son propre tour.
/// Accessible aux joueurs et aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage ChallengeWord) — non encore implémenté.
/// </remarks>
public sealed class PlayChallengeCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.challenge";

    private readonly ILogger<PlayChallengeCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayChallengeCommand(ILogger<PlayChallengeCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: appeler le cas d'usage ChallengeWord de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine("[play challenge] Non implémenté — Lama.Core absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
