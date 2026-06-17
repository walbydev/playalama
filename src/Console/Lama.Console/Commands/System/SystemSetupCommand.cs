using System.Text;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system setup</c> — initialise le système lors de la première utilisation.
/// Crée le compte SuperAdmin avec le nom d'utilisateur et le mot de passe fournis.
/// Ne peut être exécutée qu'une seule fois. Échoue si un SuperAdmin existe déjà.
/// </summary>
public sealed class SystemSetupCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.setup";

    private readonly IAccountService _accountService;
    private readonly ILogger<SystemSetupCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemSetupCommand(IAccountService accountService, ILogger<SystemSetupCommand> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (_accountService.IsInitialized)
        {
            global::System.Console.Error.WriteLine(
                "[system setup] Le système est déjà initialisé. " +
                "Un compte SuperAdmin existe déjà.");
            global::System.Console.Error.WriteLine(
                "Pour réinitialiser le mot de passe : lama system account reset-password <username>");
            return Task.FromResult(ExitCodes.GeneralError);
        }

        // Lecture interactive du username et mot de passe
        global::System.Console.Write("Nom d'utilisateur SuperAdmin : ");
        var username = global::System.Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            global::System.Console.Error.WriteLine("[system setup] Nom d'utilisateur requis.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        global::System.Console.Write("Mot de passe : ");
        var password = ReadPassword();

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            global::System.Console.Error.WriteLine(
                "[system setup] Le mot de passe doit contenir au moins 8 caractères.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        global::System.Console.Write("Confirmer le mot de passe : ");
        var confirm = ReadPassword();

        if (password != confirm)
        {
            global::System.Console.Error.WriteLine("[system setup] Les mots de passe ne correspondent pas.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        try
        {
            var account = _accountService.CreateSuperAdmin(username, password);
            _logger.LogInformation("SuperAdmin créé : {Username}", account.Username);
            global::System.Console.WriteLine();
            global::System.Console.WriteLine($"✓ Système initialisé. SuperAdmin '{account.Username}' créé.");
            global::System.Console.WriteLine("  Connectez-vous avec : lama login");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (InvalidOperationException ex)
        {
            global::System.Console.Error.WriteLine($"[system setup] Erreur : {ex.Message}");
            return Task.FromResult(ExitCodes.GeneralError);
        }
    }

    /// <summary>
    /// Lit un mot de passe sans afficher les caractères saisis (masqué par des '*').
    /// Cross-platform : fonctionne sur Windows, Linux et macOS.
    /// </summary>
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
