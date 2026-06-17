using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system restart</c> — redémarre le service applicatif.
/// Réservée aux administrateurs.
/// </summary>
public sealed class SystemRestartCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.restart";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<SystemRestartCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemRestartCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        IGameRepository gameRepository,
        ILogger<SystemRestartCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService    = sessionService;
        _gameRepository    = gameRepository;
        _logger            = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var evictedSessions = _createGameUseCase.ResetInMemorySessions();
        var restoredActiveGame = false;

        var session = _sessionService.LoadSession();
        if (session?.GameId is not null && _gameRepository.Exists(session.GameId))
        {
            _ = _createGameUseCase.GetEngine(session.GameId);
            _sessionService.SaveSession(session with { UpdatedAt = DateTimeOffset.UtcNow });
            restoredActiveGame = true;
        }

        global::System.Console.WriteLine("✓ Redémarrage logique terminé.");
        global::System.Console.WriteLine($"  Sessions mémoire purgées : {evictedSessions}");
        global::System.Console.WriteLine($"  Partie active restaurée  : {(restoredActiveGame ? "oui" : "non")}");

        _logger.LogInformation("Redémarrage logique exécuté (evicted={Evicted}, restored={Restored})",
            evictedSessions, restoredActiveGame);
        return Task.FromResult(ExitCodes.Success);
    }
}
