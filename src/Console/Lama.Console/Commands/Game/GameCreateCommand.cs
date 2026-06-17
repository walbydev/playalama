using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game create [--level casual|standard|competitive|tournament]</c>
/// — crée une nouvelle partie et initialise la session locale.
///
/// L'hôte (créateur) reçoit le rôle <see cref="Role.Host"/> dans la session.
/// La session est persistée dans <c>session.json</c> pour les commandes suivantes.
/// </summary>
public sealed class GameCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.create";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService   _sessionService;
    private readonly ILogger<GameCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameCreateCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService   sessionService,
        ILogger<GameCreateCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService    = sessionService;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Lire le nom de l'hôte (argument positionnel ou option --host)
        var hostName = context.GetArgument(0)
                    ?? context.GetOption("host")
                    ?? context.PlayerName
                    ?? "Hôte";

        // Lire le niveau de jeu
        var levelStr = context.GetOption("level") ?? "standard";
        if (!Enum.TryParse<GameLevel>(levelStr, ignoreCase: true, out var gameLevel))
        {
            global::System.Console.Error.WriteLine(
                $"[game create] Niveau invalide : '{levelStr}'. " +
                "Valeurs acceptées : casual, standard, competitive, tournament.");
            return ExitCodes.InvalidArgument;
        }

        try
        {
            var request  = new CreateGameRequest(hostName, Language: context.Lang);
            var response = await _createGameUseCase.ExecuteAsync(request);

            // Persister la session
            var session = new SessionContext(
                GameId:         response.GameId,
                PlayerId:       response.HostPlayerId,
                PlayerName:     hostName,
                Role:           Role.Host,
                GameLevel:      gameLevel,
                AuthToken:      null,
                TokenExpiresAt: null,
                CreatedAt:      DateTimeOffset.UtcNow,
                UpdatedAt:      DateTimeOffset.UtcNow);

            _sessionService.SaveSession(session);

            global::System.Console.WriteLine($"✓ Partie créée (ID : {response.GameId})");
            global::System.Console.WriteLine($"  Hôte      : {hostName}");
            global::System.Console.WriteLine($"  Niveau    : {gameLevel}");
            global::System.Console.WriteLine($"  Session   : {_sessionService.SessionFilePath}");
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Les autres joueurs peuvent rejoindre avec :");
            global::System.Console.WriteLine("  lama game join <nom>");

            _logger.LogInformation("Partie créée : {GameId} par {Host}", response.GameId, hostName);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[game create] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
