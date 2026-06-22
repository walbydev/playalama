using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.System;

/// <summary>
/// Commande <c>lama system clean</c> — nettoie toutes les parties actives.
/// Supprime les fichiers de parties persistées et réinitialise les sessions.
/// Réservée aux administrateurs.
/// </summary>
public sealed class SystemCleanCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "system.clean";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<SystemCleanCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public SystemCleanCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        IGameRepository gameRepository,
        ILogger<SystemCleanCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService    = sessionService;
        _gameRepository    = gameRepository;
        _logger            = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // Récupérer toutes les parties persistées
        var gameIds = _gameRepository.ListGameIds();
        var deletedCount = 0;

        // Supprimer chaque partie
        foreach (var gameId in gameIds)
        {
            try
            {
                _gameRepository.Delete(gameId);
                deletedCount++;
                _logger.LogDebug("Partie supprimée : {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors de la suppression de la partie {GameId}", gameId);
            }
        }

        // Réinitialiser les sessions en mémoire
        var evictedSessions = _createGameUseCase.ResetInMemorySessions();

        // Nettoyer la session actuelle
        var session = _sessionService.LoadSession();
        if (session is not null)
        {
            var cleanedSession = session with { GameId = null, UpdatedAt = DateTimeOffset.UtcNow };
            _sessionService.SaveSession(cleanedSession);
        }

        global::System.Console.WriteLine("✓ Nettoyage terminé.");
        global::System.Console.WriteLine($"  Parties supprimées    : {deletedCount}");
        global::System.Console.WriteLine($"  Sessions purgées      : {evictedSessions}");
        global::System.Console.WriteLine($"  Partie active effacée : {(session?.GameId is not null ? "oui" : "non")}");

        _logger.LogInformation("Nettoyage exécuté (deleted={Deleted}, evicted={Evicted})",
            deletedCount, evictedSessions);

        return Task.FromResult(ExitCodes.Success);
    }
}

