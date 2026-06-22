using System.Text;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system account create &lt;username&gt;</c>
/// — crée un compte Admin. Réservée au SuperAdmin.
/// Arguments : nom d'utilisateur (positionnel, requis).
/// </summary>
public sealed class SystemAccountCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.account.create";

    private readonly IAccountService _accountService;
    private readonly ILogger<SystemAccountCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemAccountCreateCommand(IAccountService accountService, ILogger<SystemAccountCreateCommand> logger)
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
                "[system account create] Argument requis : <username>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        global::System.Console.Write($"Mot de passe pour '{username}' : ");
        var password = ReadPassword();

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            global::System.Console.Error.WriteLine(
                "[system account create] Le mot de passe doit contenir au moins 8 caractères.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        global::System.Console.Write("Confirmer le mot de passe : ");
        var confirm = ReadPassword();

        if (password != confirm)
        {
            global::System.Console.Error.WriteLine(
                "[system account create] Les mots de passe ne correspondent pas.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        try
        {
            var account = _accountService.CreateAdmin(username, password);
            _logger.LogInformation("Admin créé : {Username}", account.Username);
            global::System.Console.WriteLine($"✓ Compte Admin '{account.Username}' créé.");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (InvalidOperationException ex)
        {
            global::System.Console.Error.WriteLine($"[system account create] Erreur : {ex.Message}");
            return Task.FromResult(ExitCodes.GeneralError);
        }
    }

    private static string ReadPassword()
    {
        var password = new StringBuilder();
        global::System.Console.WriteLine();
        while (true)
        {
            var key = global::System.Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                global::System.Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                global::System.Console.Write('*');
            }
        }
        global::System.Console.WriteLine();
        return password.ToString();
    }
}
