using System.Text.Json;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Rating;

/// <summary>
/// Commande <c>lama rating leaderboard [--top N]</c>.
/// Affiche le classement mondial par Elo.
/// </summary>
public sealed class RatingLeaderboardCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "rating.leaderboard";

    private readonly IPlayerRatingService _playerRatingService;
    private readonly ILogger<RatingLeaderboardCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public RatingLeaderboardCommand(
        IPlayerRatingService playerRatingService,
        ILogger<RatingLeaderboardCommand> logger)
    {
        _playerRatingService = playerRatingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var topRaw = context.GetOption("top");
        var top = 20;

        if (topRaw is not null && (!int.TryParse(topRaw, out top) || top <= 0 || top > 1000))
        {
            global::System.Console.Error.WriteLine(
                "[rating leaderboard] --top doit être un entier entre 1 et 1000.");
            return ExitCodes.InvalidArgument;
        }

        var leaderboard = await _playerRatingService.GetLeaderboardAsync(top);

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(leaderboard, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                break;

            case "csv":
                global::System.Console.WriteLine(
                    "rank,playerId,elo,level,levelName,wins,losses,abandoned,winRate");
                for (var i = 0; i < leaderboard.Count; i++)
                {
                    var player = leaderboard[i];
                    global::System.Console.WriteLine(
                        $"{i + 1},{player.PlayerId},{player.EloRating:F0},{player.Level},{EscapeCsv(player.LevelName)},{player.WinsCount},{player.LossesCount},{player.AbandonedCount},{player.WinRate:F2}");
                }
                break;

            default:
                global::System.Console.WriteLine($"=== CLASSEMENT MONDIAL (Top {top}) ===");
                if (leaderboard.Count == 0)
                {
                    global::System.Console.WriteLine("Aucun joueur classé pour le moment.");
                    break;
                }

                for (var i = 0; i < leaderboard.Count; i++)
                {
                    var p = leaderboard[i];
                    global::System.Console.WriteLine(
                        $"{i + 1,2}. {p.PlayerId,-24} {p.EloRating,5:F0} Elo  {p.LevelName}");
                }
                break;
        }

        _logger.LogInformation("Leaderboard consulté (top={Top}, format={Format})", top, context.OutputFormat);
        return ExitCodes.Success;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}

