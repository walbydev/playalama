using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system account revoke &lt;username&gt;</c>
/// — révoque un compte Admin. Réservée au SuperAdmin.
/// Le compte est désactivé mais conservé dans accounts.json pour l'audit.
/// </summary>
public sealed class SystemAccountRevokeCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.account.revoke";

    private readonly IAccountService _accountService;
    private readonly ILogger<SystemAccountRevokeCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemAccountRevokeCommand(IAccountService accountService, ILogger<SystemAccountRevokeCommand> logger)
    {
        _accountService = accountService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var username = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(username))
        {
            global::System.Console.Error.WriteLine(
                "[system account revoke] Argument requis : <username>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        try
        {
            var revoked = _accountService.Revoke(username);
            if (!revoked)
            {
                global::System.Console.Error.WriteLine(
                    $"[system account revoke] Compte introuvable : '{username}'.");
                return Task.FromResult(ExitCodes.GeneralError);
            }

            _logger.LogInformation("Compte révoqué : {Username}", username);
            global::System.Console.WriteLine($"✓ Compte '{username}' révoqué.");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (InvalidOperationException ex)
        {
            global::System.Console.Error.WriteLine($"[system account revoke] Erreur : {ex.Message}");
            return Task.FromResult(ExitCodes.GeneralError);
        }
    }
}
