using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system restart</c> — redémarre le service applicatif.
/// Réservée aux administrateurs.
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Infrastructure (gestion du cycle de vie) — non encore implémenté.
/// </remarks>
public sealed class SystemRestartCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.restart";

    private readonly ILogger<SystemRestartCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemRestartCommand(ILogger<SystemRestartCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: implémenter le redémarrage via Lama.Infrastructure
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Infrastructure absent)", CommandId);
        global::System.Console.Error.WriteLine("[system restart] Non implémenté — Lama.Infrastructure absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
