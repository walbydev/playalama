using System.Text.Json;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Rating;

/// <summary>
/// Commande <c>lama rating leaderboard [--queue open|tournament|global] [--top N]</c>.
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
        var queue = ParseQueue(context.GetOption("queue"), out var queueError);
        if (queueError is not null)
        {
            global::System.Console.Error.WriteLine(queueError);
            return ExitCodes.InvalidArgument;
        }

        var topRaw = context.GetOption("top");
        var top = 20;

        if (topRaw is not null && (!int.TryParse(topRaw, out top) || top <= 0 || top > 1000))
        {
            global::System.Console.Error.WriteLine(
                "[rating leaderboard] --top doit être un entier entre 1 et 1000.");
            return ExitCodes.InvalidArgument;
        }

        var leaderboard = await _playerRatingService.GetLeaderboardAsync(queue, top);

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
                    "rank,playerId,queue,elo,level,levelName,wins,losses,abandoned,winRate");
                for (var i = 0; i < leaderboard.Count; i++)
                {
                    var player = leaderboard[i];
                    var queueElo = ResolveQueueElo(player, queue);
                    global::System.Console.WriteLine(
                        $"{i + 1},{player.PlayerId},{queue.ToString().ToLowerInvariant()},{queueElo:F0},{player.Level},{EscapeCsv(player.LevelName)},{player.WinsCount},{player.LossesCount},{player.AbandonedCount},{player.WinRate:F2}");
                }
                break;

            default:
                var queueLabel = queue switch
                {
                    RankingQueue.OpenRanked => "OPEN",
                    RankingQueue.Tournament => "TOURNOI",
                    _ => "GLOBAL"
                };

                global::System.Console.WriteLine($"=== CLASSEMENT {queueLabel} (Top {top}) ===");
                if (leaderboard.Count == 0)
                {
                    global::System.Console.WriteLine("Aucun joueur classé pour le moment.");
                    break;
                }

                for (var i = 0; i < leaderboard.Count; i++)
                {
                    var p = leaderboard[i];
                    var queueElo = ResolveQueueElo(p, queue);
                    global::System.Console.WriteLine(
                        $"{i + 1,2}. {p.PlayerId,-24} {queueElo,5:F0} Elo  {p.LevelName}");
                }
                break;
        }

        _logger.LogInformation(
            "Leaderboard consulté (queue={Queue}, top={Top}, format={Format})",
            queue,
            top,
            context.OutputFormat);
        return ExitCodes.Success;
    }

    private static RankingQueue ParseQueue(string? raw, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return RankingQueue.GlobalPrestige;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "open":
            case "ranked":
                return RankingQueue.OpenRanked;
            case "tournament":
            case "tournoi":
                return RankingQueue.Tournament;
            case "global":
                return RankingQueue.GlobalPrestige;
            default:
                error = "[rating leaderboard] --queue doit valoir open|tournament|global.";
                return RankingQueue.GlobalPrestige;
        }
    }

    private static double ResolveQueueElo(PlayerRating rating, RankingQueue queue) =>
        queue switch
        {
            RankingQueue.OpenRanked => rating.EloOpen,
            RankingQueue.Tournament => rating.EloTournament,
            _ => rating.GlobalPrestige
        };

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}

