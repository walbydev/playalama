using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Tournament;

/// <summary>
/// Commande <c>lama tournament create &lt;nom&gt;</c> — crée un tournoi.
/// Arguments : nom du tournoi (positionnel, requis).
/// Accessible aux joueurs et aux admins.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Core (cas d'usage CreateTournament) — non encore implémenté.
/// </remarks>
public sealed class TournamentCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "tournament.create";

    private readonly ILogger<TournamentCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public TournamentCreateCommand(ILogger<TournamentCreateCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var tournamentName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(tournamentName))
        {
            global::System.Console.Error.WriteLine("[tournament create] Argument requis : <nom>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // TODO: appeler le cas d'usage CreateTournament de Lama.Core
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Core absent)", CommandId);
        global::System.Console.Error.WriteLine(
            $"[tournament create] Non implémenté — Lama.Core absent. (nom : {tournamentName})");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
