using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Tournament;

/// <summary>
/// Commande <c>lama tournament create &lt;nom&gt;</c> — crée un tournoi.
/// Arguments : nom du tournoi (positionnel, requis).
/// Accessible aux joueurs et aux admins.
/// </summary>
public sealed class TournamentCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "tournament.create";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly ILogger<TournamentCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public TournamentCreateCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        ILogger<TournamentCreateCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService    = sessionService;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var tournamentName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(tournamentName))
        {
            global::System.Console.Error.WriteLine("[tournament create] Argument requis : <nom>");
            return ExitCodes.InvalidArgument;
        }

        var hostName = context.GetOption("host")
            ?? context.PlayerName
            ?? "Hôte";

        try
        {
            var response = await _createGameUseCase.ExecuteAsync(new CreateGameRequest(
                hostPlayerName: hostName,
                language: context.Lang,
                gameLevel: GameLevel.Tournament));

            var current = _sessionService.LoadSession();
            var now = DateTimeOffset.UtcNow;
            var session = new SessionContext(
                GameId:         response.GameId,
                PlayerId:       response.HostPlayerId,
                PlayerName:     hostName,
                Role:           Role.Host,
                GameLevel:      GameLevel.Tournament,
                AuthToken:      current?.AuthToken,
                TokenExpiresAt: current?.TokenExpiresAt,
                CreatedAt:      current?.CreatedAt ?? now,
                UpdatedAt:      now);

            _sessionService.SaveSession(session);

            global::System.Console.WriteLine($"✓ Tournoi créé : {tournamentName}");
            global::System.Console.WriteLine($"  Partie support : {response.GameId}");
            global::System.Console.WriteLine($"  Hôte           : {hostName}");
            global::System.Console.WriteLine($"  Niveau         : Tournament");

            _logger.LogInformation("Tournoi '{Tournament}' créé via partie {GameId}",
                tournamentName, response.GameId);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[tournament create] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
