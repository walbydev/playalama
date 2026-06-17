using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama logout</c> — déconnecte l'utilisateur courant.
/// Efface le token de la session sans supprimer la session de partie.
/// </summary>
public sealed class SystemLogoutCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "logout";

    private readonly IAuthService _authService;
    private readonly ILogger<SystemLogoutCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemLogoutCommand(IAuthService authService, ILogger<SystemLogoutCommand> logger)
    {
        _authService = authService;
        _logger      = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        _authService.Logout();
        global::System.Console.WriteLine("✓ Déconnecté.");
        _logger.LogInformation("Logout effectué.");
        return Task.FromResult(ExitCodes.Success);
    }
}
