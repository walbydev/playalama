using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game show</c> — affiche les informations de la partie courante.
/// Accessible à tous les rôles (lecture seule).
/// Options : --output (text|json|csv).
/// </summary>
public sealed class GameShowCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.show";

    private readonly IGameRepository _gameRepository;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<GameShowCommand> _logger;

    /// <summary>
    /// Constructeur de rétrocompatibilité (mode local uniquement).
    /// </summary>
    public GameShowCommand(IGameRepository gameRepository, ILogger<GameShowCommand> logger)
        : this(
            gameRepository,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public GameShowCommand(
        IGameRepository gameRepository,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<GameShowCommand> logger)
    {
        _gameRepository = gameRepository;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var gameId = context.GetArgument(0) ?? context.GameId;
        if (string.IsNullOrWhiteSpace(gameId))
        {
            global::System.Console.Error.WriteLine(
                "[game show] Aucun gameId fourni et aucune session active.");
            return ExitCodes.InvalidArgument;
        }

        if (_runtimeMode.IsOnline)
        {
            try
            {
                var onlineGame = await _onlineGameGateway.GetGameAsync(gameId, cancellationToken);

                var onlineCurrentPlayer =
                    onlineGame.CurrentPlayerIndex >= 0 && onlineGame.CurrentPlayerIndex < onlineGame.Players.Count
                        ? onlineGame.Players[onlineGame.CurrentPlayerIndex].PlayerName
                        : "inconnu";

                switch (context.OutputFormat.ToLowerInvariant())
                {
                    case "json":
                        global::System.Console.WriteLine(JsonSerializer.Serialize(onlineGame));
                        break;

                    case "csv":
                        global::System.Console.WriteLine("gameId,level,queue,isGameOver,turnNumber,currentPlayer,players,moves,tilesOnBoard,createdAt,updatedAt");
                        global::System.Console.WriteLine(
                            $"{onlineGame.Id},{onlineGame.GameLevel},{onlineGame.Queue},{onlineGame.IsGameOver},{onlineGame.TurnNumber},{onlineCurrentPlayer},{onlineGame.Players.Count},{onlineGame.Moves.Count},{onlineGame.Board.Count},{onlineGame.CreatedAt:O},{onlineGame.UpdatedAt:O}");
                        break;

                    default:
                        global::System.Console.WriteLine($"Partie online : {onlineGame.Id}");
                        global::System.Console.WriteLine($"  Niveau        : {onlineGame.GameLevel}");
                        global::System.Console.WriteLine($"  Queue         : {onlineGame.Queue}");
                        global::System.Console.WriteLine($"  Statut        : {(onlineGame.IsGameOver ? "terminee" : "active")}");
                        global::System.Console.WriteLine($"  Tour          : {onlineGame.TurnNumber}");
                        global::System.Console.WriteLine($"  Joueur courant: {onlineCurrentPlayer}");
                        global::System.Console.WriteLine($"  Coups joues   : {onlineGame.Moves.Count}");
                        global::System.Console.WriteLine($"  Tuiles posees : {onlineGame.Board.Count}");
                        global::System.Console.WriteLine("  Joueurs       :");

                        foreach (var player in onlineGame.Players)
                        {
                            global::System.Console.WriteLine(
                                $"    - {player.PlayerName,-15} score: {player.Score,4} | rack: {player.RackCount} lettres | id: {player.PlayerId}");
                        }
                        break;
                }

                _logger.LogInformation("{CommandId} online : details affiches pour {GameId}", CommandId, onlineGame.Id);
                return ExitCodes.Success;
            }
            catch (HttpRequestException ex)
            {
                global::System.Console.Error.WriteLine($"[game show] Erreur online : {ex.Message}");
                return ExitCodes.GeneralError;
            }
        }

        var game = _gameRepository.Load(gameId);
        if (game is null)
        {
            global::System.Console.Error.WriteLine($"[game show] Partie introuvable : {gameId}");
            return ExitCodes.GameNotFound;
        }

        var currentPlayer =
            game.CurrentPlayerIndex >= 0 && game.CurrentPlayerIndex < game.Players.Count
                ? game.Players[game.CurrentPlayerIndex].Name
                : "inconnu";

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(new
                {
                    game.GameId,
                    game.Language,
                    Level = game.GameLevel,
                    game.IsGameOver,
                    game.TurnNumber,
                    game.CurrentPlayerIndex,
                    CurrentPlayer = currentPlayer,
                    Players = game.Players.Select(p => new { p.PlayerId, p.Name, p.Score, RackCount = p.Rack.Count }),
                    TilesOnBoard = game.Board.Count,
                    game.CreatedAt,
                    game.UpdatedAt
                }));
                break;

            case "csv":
                global::System.Console.WriteLine("gameId,language,level,isGameOver,turnNumber,currentPlayer,players,tilesOnBoard,updatedAt");
                global::System.Console.WriteLine(
                    $"{game.GameId},{game.Language},{game.GameLevel},{game.IsGameOver},{game.TurnNumber},{currentPlayer},{game.Players.Count},{game.Board.Count},{game.UpdatedAt:O}");
                break;

            default:
                global::System.Console.WriteLine($"Partie : {game.GameId}");
                global::System.Console.WriteLine($"  Langue        : {game.Language}");
                global::System.Console.WriteLine($"  Niveau        : {game.GameLevel}");
                global::System.Console.WriteLine($"  Statut        : {(game.IsGameOver ? "terminee" : "active")}");
                global::System.Console.WriteLine($"  Tour          : {game.TurnNumber}");
                global::System.Console.WriteLine($"  Joueur courant: {currentPlayer}");
                global::System.Console.WriteLine($"  Tuiles posees : {game.Board.Count}");
                global::System.Console.WriteLine("  Joueurs       :");

                foreach (var player in game.Players)
                {
                    global::System.Console.WriteLine(
                        $"    - {player.Name,-15} score: {player.Score,4} | rack: {player.Rack.Count} lettres");
                }

                break;
        }

        _logger.LogInformation("{CommandId} : details affiches pour {GameId}", CommandId, game.GameId);
        return ExitCodes.Success;
    }
}
