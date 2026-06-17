using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system status</c> — affiche l'état du système et des services.
/// Réservée aux administrateurs.
/// Options : --output (text|json|csv).
/// </summary>
public sealed class SystemStatusCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.status";

    private readonly IAccountService _accountService;
    private readonly ISessionService _sessionService;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<SystemStatusCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemStatusCommand(
        IAccountService accountService,
        ISessionService sessionService,
        IGameRepository gameRepository,
        ILogger<SystemStatusCommand> logger)
    {
        _accountService = accountService;
        _sessionService = sessionService;
        _gameRepository = gameRepository;
        _logger         = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var allAccounts = _accountService.GetAll();
        var activeAccounts = allAccounts.Count(a => a.Active);
        var session = _sessionService.LoadSession();
        var gameIds = _gameRepository.ListGameIds();

        var status = new
        {
            utcNow = DateTimeOffset.UtcNow,
            isInitialized = _accountService.IsInitialized,
            accounts = new { total = allAccounts.Count, active = activeAccounts },
            games = new { persisted = gameIds.Count },
            session = new
            {
                hasSession = session is not null,
                session?.Role,
                session?.PlayerName,
                session?.GameId,
                session?.UpdatedAt,
                sessionFile = _sessionService.SessionFilePath
            }
        };

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                break;

            case "csv":
                global::System.Console.WriteLine(
                    "utcNow,isInitialized,accountsTotal,accountsActive,persistedGames,hasSession,role,playerName,gameId,sessionFile");
                global::System.Console.WriteLine(
                    $"{status.utcNow:O},{status.isInitialized.ToString().ToLowerInvariant()},{allAccounts.Count},{activeAccounts},{gameIds.Count},{(session is not null).ToString().ToLowerInvariant()},{session?.Role},{EscapeCsv(session?.PlayerName)},{session?.GameId},{EscapeCsv(_sessionService.SessionFilePath)}");
                break;

            default:
                global::System.Console.WriteLine("=== SYSTEM STATUS ===");
                global::System.Console.WriteLine($"UTC               : {status.utcNow:O}");
                global::System.Console.WriteLine($"Initialisé        : {(status.isInitialized ? "oui" : "non")}");
                global::System.Console.WriteLine($"Comptes actifs    : {activeAccounts}/{allAccounts.Count}");
                global::System.Console.WriteLine($"Parties persistées: {gameIds.Count}");
                global::System.Console.WriteLine($"Session active    : {(session is not null ? "oui" : "non")}");
                if (session is not null)
                {
                    global::System.Console.WriteLine($"  Rôle            : {session.Role}");
                    global::System.Console.WriteLine($"  Joueur          : {session.PlayerName ?? "(aucun)"}");
                    global::System.Console.WriteLine($"  Partie          : {session.GameId ?? "(aucune)"}");
                }
                global::System.Console.WriteLine($"Fichier session   : {_sessionService.SessionFilePath}");
                break;
        }

        _logger.LogInformation("Statut système consulté ({Format})", context.OutputFormat);
        return Task.FromResult(ExitCodes.Success);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
