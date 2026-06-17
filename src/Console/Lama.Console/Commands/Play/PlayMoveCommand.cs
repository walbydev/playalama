using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play move &lt;case&gt; &lt;mot&gt; &lt;direction&gt;</c> — pose un mot sur le plateau.
/// Arguments positionnels : case (ex: H8), mot (ex: MAISON), direction (H ou V).
/// Options : --joker N=L (le joker à la position N représente la lettre L),
///           --dry-run (simule le coup sans le jouer).
/// Accessible aux joueurs et aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (IGameEngine.PlayMove / ValidateMove) — non encore implémenté.
/// </remarks>
public sealed class PlayMoveCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.move";

    private readonly ILogger<PlayMoveCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayMoveCommand(ILogger<PlayMoveCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var position = context.GetArgument(0);  // ex: H8
        var word     = context.GetArgument(1);  // ex: MAISON
        var direction = context.GetArgument(2); // H ou V

        if (string.IsNullOrWhiteSpace(position) ||
            string.IsNullOrWhiteSpace(word) ||
            string.IsNullOrWhiteSpace(direction))
        {
            global::System.Console.Error.WriteLine(
                "[play move] Arguments requis : <case> <mot> <direction>");
            global::System.Console.Error.WriteLine("  Exemple : lama play move H8 MAISON H");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        if (!direction.Equals("H", StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals("V", StringComparison.OrdinalIgnoreCase))
        {
            global::System.Console.Error.WriteLine(
                "[play move] Direction invalide : utilisez H (horizontal) ou V (vertical).");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        var isDryRun = context.HasOption("dry-run");

        // TODO: appeler IGameEngine.ValidateMove() puis PlayMove() via Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent) — {Position} {Word} {Direction}",
            CommandId, position, word, direction);
        global::System.Console.Error.WriteLine(
            $"[play move] Non implémenté — Lama.Core absent. " +
            $"({position} {word} {direction}{(isDryRun ? " --dry-run" : "")})");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
