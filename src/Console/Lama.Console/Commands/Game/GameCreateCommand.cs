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
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<GameCreateCommand> _logger;

    /// <summary>
    /// Constructeur de rétrocompatibilité (mode local uniquement).
    /// </summary>
    public GameCreateCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        ILogger<GameCreateCommand> logger)
        : this(
            createGameUseCase,
            sessionService,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public GameCreateCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService   sessionService,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<GameCreateCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService    = sessionService;
        _runtimeMode       = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
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

        var modeStr = context.GetOption("mode") ?? (_runtimeMode.IsOnline ? "multi" : "solo");
        var explicitMode = context.GetOption("mode");
        if (!modeStr.Equals("solo", StringComparison.OrdinalIgnoreCase) &&
            !modeStr.Equals("multi", StringComparison.OrdinalIgnoreCase))
        {
            global::System.Console.Error.WriteLine("[game create] --mode invalide. Valeurs acceptées : solo, multi.");
            return ExitCodes.InvalidArgument;
        }

        if (!_runtimeMode.IsOnline && modeStr.Equals("multi", StringComparison.OrdinalIgnoreCase))
        {
            global::System.Console.Error.WriteLine("[game create] Le mode multi est disponible uniquement en mode serveur (online).");
            return ExitCodes.InvalidArgument;
        }

        var gameName = context.GetOption("name");
        var isPrivate = context.HasOption("private");
        var password = context.GetOption("password");
        var enableAi = context.HasOption("with-ai");

        int? maxPlayers = null;
        var maxPlayersRaw = context.GetOption("max-players");
        if (!string.IsNullOrWhiteSpace(maxPlayersRaw))
        {
            if (!int.TryParse(maxPlayersRaw, out var parsedMaxPlayers))
            {
                global::System.Console.Error.WriteLine("[game create] --max-players doit être un entier.");
                return ExitCodes.InvalidArgument;
            }

            maxPlayers = parsedMaxPlayers;
        }

        try
        {
            string gameId;
            string hostPlayerId;
            List<char>? initialRack = null;
            OnlineLoginResponse? loginResponse = null;
            var existingSession = _sessionService.LoadSession();

            if (_runtimeMode.IsOnline)
            {
                _onlineGameGateway.SetAuthToken(existingSession?.AuthToken);
                loginResponse = await _onlineGameGateway.EnsureAuthenticatedAsync(
                    hostName,
                    existingSession?.PlayerId,
                    cancellationToken);

                var response = await _onlineGameGateway.CreateGameAsync(
                    hostName,
                    gameLevel,
                    context.Lang,
                    explicitMode,
                    gameName,
                    isPrivate,
                    password,
                    maxPlayers,
                    enableAi,
                    cancellationToken);
                gameId = response.GameId;
                hostPlayerId = response.HostPlayerId;
                initialRack = response.Rack;
            }
            else
            {
                var request  = new CreateGameRequest(hostName, language: context.Lang, gameLevel: gameLevel);
                var response = await _createGameUseCase.ExecuteAsync(request);
                gameId = response.GameId;
                hostPlayerId = response.HostPlayerId;
            }

            // Persister la session
            var session = new SessionContext(
                GameId:         gameId,
                PlayerId:       hostPlayerId,
                PlayerName:     hostName,
                Role:           Role.Host,
                GameLevel:      gameLevel,
                AuthToken:      _runtimeMode.IsOnline ? _onlineGameGateway.GetAuthToken() : existingSession?.AuthToken,
                TokenExpiresAt: _runtimeMode.IsOnline ? loginResponse?.ExpiresAt : existingSession?.TokenExpiresAt,
                CreatedAt:      existingSession?.CreatedAt ?? DateTimeOffset.UtcNow,
                UpdatedAt:      DateTimeOffset.UtcNow);

            _sessionService.SaveSession(session);

            global::System.Console.WriteLine($"✓ Partie créée (ID : {gameId})");
            global::System.Console.WriteLine($"  Mode      : {(_runtimeMode.IsOnline ? "online" : "local")}");
            global::System.Console.WriteLine($"  Type      : {modeStr}");
            global::System.Console.WriteLine($"  Hôte      : {hostName}");
            global::System.Console.WriteLine($"  Niveau    : {gameLevel}");
            if (!string.IsNullOrWhiteSpace(gameName))
                global::System.Console.WriteLine($"  Nom       : {gameName}");
            if (_runtimeMode.IsOnline)
                global::System.Console.WriteLine($"  IA        : {(enableAi ? "activée" : "désactivée")}");
            if (initialRack is not null)
                global::System.Console.WriteLine($"  Rack      : {string.Join(" ", initialRack)}");
            global::System.Console.WriteLine($"  Session   : {_sessionService.SessionFilePath}");
            global::System.Console.WriteLine();
            global::System.Console.WriteLine("Les autres joueurs peuvent rejoindre avec :");
            global::System.Console.WriteLine("  lama game join <nom>");

            _logger.LogInformation("Partie créée : {GameId} par {Host}", gameId, hostName);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[game create] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
        catch (HttpRequestException ex)
        {
            global::System.Console.Error.WriteLine($"[game create] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
