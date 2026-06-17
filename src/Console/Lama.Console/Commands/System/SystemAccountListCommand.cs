using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system account list</c> — liste tous les comptes.
/// Réservée au SuperAdmin.
/// Options : --output (text|json|csv).
/// </summary>
public sealed class SystemAccountListCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.account.list";

    private readonly IAccountService _accountService;
    private readonly ILogger<SystemAccountListCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemAccountListCommand(IAccountService accountService, ILogger<SystemAccountListCommand> logger)
    {
        _accountService = accountService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var accounts = _accountService.GetAll();

        if (context.OutputFormat == "json")
        {
            var json = "[" + string.Join(",", accounts.Select(a =>
                $"{{\"username\":\"{a.Username}\",\"role\":\"{a.Role}\",\"active\":{a.Active.ToString().ToLower()},\"createdAt\":\"{a.CreatedAt:O}\"}}")) + "]";
            global::System.Console.WriteLine(json);
            return Task.FromResult(ExitCodes.Success);
        }

        global::System.Console.WriteLine($"{"Nom",-20} {"Rôle",-12} {"Actif",-6} {"Créé le"}");
        global::System.Console.WriteLine(new string('-', 60));

        foreach (var a in accounts.OrderBy(x => x.Role).ThenBy(x => x.Username))
        {
            var status = a.Active ? "✓" : "✗";
            global::System.Console.WriteLine(
                $"{a.Username,-20} {a.Role,-12} {status,-6} {a.CreatedAt:dd/MM/yyyy HH:mm}");
        }

        global::System.Console.WriteLine($"\n{accounts.Count} compte(s).");
        return Task.FromResult(ExitCodes.Success);
    }
}
