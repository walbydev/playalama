using System.Text;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama login [--username &lt;nom&gt;]</c> — authentifie un Admin ou SuperAdmin.
/// Écrit le token dans la session. Sans --username, demande une saisie interactive.
/// </summary>
public sealed class SystemLoginCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "login";

    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<SystemLoginCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemLoginCommand(
        IAuthService authService,
        ISessionService sessionService,
        ILogger<SystemLoginCommand> logger)
    {
        _authService    = authService;
        _sessionService = sessionService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var username = context.GetOption("username") ?? context.GetOption("u");

        if (string.IsNullOrWhiteSpace(username))
        {
            global::System.Console.Write("Nom d'utilisateur : ");
            username = global::System.Console.ReadLine()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            global::System.Console.Error.WriteLine("[login] Nom d'utilisateur requis.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        global::System.Console.Write("Mot de passe : ");
        var password = ReadPassword();

        var result = _authService.Login(username, password);

        if (!result.Success)
        {
            global::System.Console.Error.WriteLine($"[login] Échec : {result.ErrorMessage}");
            return Task.FromResult(ExitCodes.AccessDenied);
        }

        // Écrire le token dans la session (en conservant une éventuelle session de partie)
        var existing = _sessionService.LoadSession();
        var session  = new SessionContext(
            GameId:        existing?.GameId,
            PlayerId:      existing?.PlayerId,
            PlayerName:    result.Account!.Username,
            Role:          result.Account.Role,
            GameLevel:     existing?.GameLevel,
            AuthToken:     result.Token,
            TokenExpiresAt: result.ExpiresAt,
            CreatedAt:     existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt:     DateTimeOffset.UtcNow);

        _sessionService.SaveSession(session);

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            $"✓ Connecté en tant que {result.Account.Username} ({result.Account.Role}).");

        _logger.LogInformation("Login : {Username} ({Role})", result.Account.Username, result.Account.Role);
        return Task.FromResult(ExitCodes.Success);
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
