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
    private readonly ILogger<GameShowCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameShowCommand(IGameRepository gameRepository, ILogger<GameShowCommand> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var gameId = context.GetArgument(0) ?? context.GameId;
        if (string.IsNullOrWhiteSpace(gameId))
        {
            global::System.Console.Error.WriteLine(
                "[game show] Aucun gameId fourni et aucune session active.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        var game = _gameRepository.Load(gameId);
        if (game is null)
        {
            global::System.Console.Error.WriteLine($"[game show] Partie introuvable : {gameId}");
            return Task.FromResult(ExitCodes.GameNotFound);
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
        return Task.FromResult(ExitCodes.Success);
    }
}
