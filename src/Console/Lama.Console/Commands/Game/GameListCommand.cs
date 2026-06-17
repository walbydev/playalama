using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game list</c> — liste les parties disponibles.
/// Accessible à tous les rôles (lecture seule).
/// Options : --output (text|json|csv).
/// </summary>
public sealed class GameListCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.list";

    private readonly IGameRepository _gameRepository;
    private readonly ILogger<GameListCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameListCommand(IGameRepository gameRepository, ILogger<GameListCommand> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var games = _gameRepository.ListGameIds()
            .Select(id => _gameRepository.Load(id))
            .Where(game => game is not null)
            .Cast<PersistedGame>()
            .OrderByDescending(g => g.UpdatedAt)
            .ToList();

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(games.Select(g => new
                {
                    g.GameId,
                    Level = g.GameLevel,
                    Players = g.Players.Count,
                    g.IsGameOver,
                    g.TurnNumber,
                    g.UpdatedAt
                })));
                break;

            case "csv":
                global::System.Console.WriteLine("gameId,level,players,isGameOver,turnNumber,updatedAt");
                foreach (var game in games)
                    global::System.Console.WriteLine(
                        $"{game.GameId},{game.GameLevel},{game.Players.Count},{game.IsGameOver},{game.TurnNumber},{game.UpdatedAt:O}");
                break;

            default:
                if (games.Count == 0)
                {
                    global::System.Console.WriteLine("Aucune partie persistée.");
                    return Task.FromResult(ExitCodes.Success);
                }

                global::System.Console.WriteLine("Parties disponibles :");
                foreach (var game in games)
                {
                    var status = game.IsGameOver ? "terminee" : "active";
                    global::System.Console.WriteLine(
                        $"- {game.GameId} | {game.GameLevel,-11} | joueurs: {game.Players.Count,2} | {status,-8} | tour {game.TurnNumber}");
                }
                break;
        }

        _logger.LogInformation("{CommandId} : {Count} partie(s) retournee(s)", CommandId, games.Count);
        return Task.FromResult(ExitCodes.Success);
    }
}
