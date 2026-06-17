using Lama.Console.Services;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system status</c> — affiche l'état du système et des services.
/// Réservée aux administrateurs.
/// Options : --output (text|json|csv).
/// </summary>
/// <remarks>
/// TODO: dépend de Lama.Infrastructure (health checks) — non encore implémenté.
/// </remarks>
public sealed class SystemStatusCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.status";

    private readonly ILogger<SystemStatusCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemStatusCommand(ILogger<SystemStatusCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // TODO: interroger les health checks de Lama.Infrastructure
        _logger.LogWarning("{CommandId} : non implémenté (Lama.Infrastructure absent)", CommandId);
        global::System.Console.Error.WriteLine("[system status] Non implémenté — Lama.Infrastructure absent.");
        return Task.FromResult(ExitCodes.GeneralError);
    }
}
