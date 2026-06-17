using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play swap &lt;lettres&gt;</c> — échange des lettres avec le sac.
/// Arguments : lettres à échanger (ex: AEI), ou --all pour échanger tout le rack.
/// Accessible aux joueurs et aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage SwapLetters) — non encore implémenté.
/// </remarks>
public sealed class PlaySwapCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.swap";

    private readonly ILogger<PlaySwapCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlaySwapCommand(ILogger<PlaySwapCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var swapAll  = context.HasOption("all");
        var letters  = context.GetArgument(0);

        if (!swapAll && string.IsNullOrWhiteSpace(letters))
        {
            global::System.Console.Error.WriteLine(
                "[play swap] Argument requis : <lettres> ou --all");
            global::System.Console.Error.WriteLine(
                "  Exemple : lama play swap AEI");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // TODO: appeler le cas d'usage SwapLetters de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine(
            $"[play swap] Non implémenté — Lama.Core absent. " +
            $"({(swapAll ? "--all" : letters)})");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
