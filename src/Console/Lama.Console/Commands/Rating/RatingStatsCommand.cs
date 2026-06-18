using System.Text.Json;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Rating;

/// <summary>
/// Commande <c>lama rating stats [playerId] [--7d|--30d|--365d]</c>.
/// Affiche les statistiques d'un joueur.
/// </summary>
public sealed class RatingStatsCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "rating.stats";

    private readonly IPlayerRatingService _playerRatingService;
    private readonly ILogger<RatingStatsCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public RatingStatsCommand(
        IPlayerRatingService playerRatingService,
        ILogger<RatingStatsCommand> logger)
    {
        _playerRatingService = playerRatingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var playerId = context.GetArgument(0) ?? context.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            global::System.Console.Error.WriteLine(
                "[rating stats] PlayerId requis (argument ou session active).");
            return ExitCodes.InvalidArgument;
        }

        var stats = await _playerRatingService.GetStatisticsAsync(playerId);

        var scope = context.HasOption("7d") ? "7d"
            : context.HasOption("30d") ? "30d"
            : context.HasOption("365d") ? "365d"
            : "all";

        var period = scope switch
        {
            "7d" => stats.Last7Days,
            "30d" => stats.Last30Days,
            "365d" => stats.Last365Days,
            _ => stats.All
        };

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(new
                {
                    stats.PlayerId,
                    scope,
                    wins = period.Wins,
                    losses = period.Losses,
                    abandoned = period.Abandoned,
                    totalGames = period.TotalGames,
                    winRate = period.WinRate,
                    highScore = period.HighScore,
                    averageScore = period.AverageScore
                }, new JsonSerializerOptions { WriteIndented = true }));
                break;

            case "csv":
                global::System.Console.WriteLine(
                    "playerId,scope,wins,losses,abandoned,totalGames,winRate,highScore,averageScore");
                global::System.Console.WriteLine(
                    $"{stats.PlayerId},{scope},{period.Wins},{period.Losses},{period.Abandoned},{period.TotalGames},{period.WinRate:F2},{period.HighScore},{period.AverageScore:F2}");
                break;

            default:
                global::System.Console.WriteLine("=== STATISTIQUES JOUEUR ===");
                global::System.Console.WriteLine($"Joueur      : {stats.PlayerId}");
                global::System.Console.WriteLine($"Periode     : {scope}");
                global::System.Console.WriteLine($"Bilan       : {period.Wins}V / {period.Losses}D / {period.Abandoned}A");
                global::System.Console.WriteLine($"Parties     : {period.TotalGames}");
                global::System.Console.WriteLine($"WinRate     : {period.WinRate:F1}%");
                global::System.Console.WriteLine($"High score  : {period.HighScore}");
                global::System.Console.WriteLine($"Score moyen : {period.AverageScore:F1}");
                break;
        }

        _logger.LogInformation(
            "Stats consultées: {PlayerId} ({Scope}, {Format})",
            playerId,
            scope,
            context.OutputFormat);

        return ExitCodes.Success;
    }
}

