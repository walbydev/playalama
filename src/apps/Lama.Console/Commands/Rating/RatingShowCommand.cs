using System.Text.Json;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Rating;

/// <summary>
/// Commande <c>lama rating show [playerId]</c>.
/// Affiche le rating global d'un joueur.
/// </summary>
public sealed class RatingShowCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "rating.show";

    private readonly IPlayerRatingService _playerRatingService;
    private readonly ILogger<RatingShowCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public RatingShowCommand(
        IPlayerRatingService playerRatingService,
        ILogger<RatingShowCommand> logger)
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
                "[rating show] PlayerId requis (argument ou session active).");
            return ExitCodes.InvalidArgument;
        }

        var rating = await _playerRatingService.GetRatingAsync(playerId);

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(new
                {
                    rating.PlayerId,
                    rating.EloRating,
                    rating.EloOpen,
                    rating.EloTournament,
                    rating.GlobalPrestige,
                    rating.Level,
                    rating.LevelName,
                    rating.WinsCount,
                    rating.LossesCount,
                    rating.AbandonedCount,
                    rating.WinRate,
                    rating.CurrentStreak,
                    rating.HighestStreak,
                    rating.HighScore,
                    rating.AverageScore,
                    rating.LastGameAt,
                    rating.UpdatedAt
                }, new JsonSerializerOptions { WriteIndented = true }));
                break;

            case "csv":
                global::System.Console.WriteLine(
                    "playerId,eloOpen,eloTournament,globalPrestige,level,levelName,wins,losses,abandoned,winRate,currentStreak,highestStreak,highScore,averageScore,lastGameAt,updatedAt");
                global::System.Console.WriteLine(
                    $"{rating.PlayerId},{rating.EloOpen:F0},{rating.EloTournament:F0},{rating.GlobalPrestige:F0},{rating.Level},{EscapeCsv(rating.LevelName)},{rating.WinsCount},{rating.LossesCount},{rating.AbandonedCount},{rating.WinRate:F2},{rating.CurrentStreak},{rating.HighestStreak},{rating.HighScore},{rating.AverageScore:F2},{rating.LastGameAt:O},{rating.UpdatedAt:O}");
                break;

            default:
                global::System.Console.WriteLine("=== RATING JOUEUR ===");
                global::System.Console.WriteLine($"Joueur      : {rating.PlayerId}");
                global::System.Console.WriteLine($"Niveau      : {rating.LevelName} (#{rating.Level})");
                global::System.Console.WriteLine($"Elo Open    : {rating.EloOpen:F0}");
                global::System.Console.WriteLine($"Elo Tournoi : {rating.EloTournament:F0}");
                global::System.Console.WriteLine($"Global      : {rating.GlobalPrestige:F0}");
                global::System.Console.WriteLine($"Bilan       : {rating.WinsCount}V / {rating.LossesCount}D / {rating.AbandonedCount}A");
                global::System.Console.WriteLine($"WinRate     : {rating.WinRate:F1}%");
                global::System.Console.WriteLine($"Serie       : {rating.CurrentStreak:+#;-#;0} (max {rating.HighestStreak})");
                global::System.Console.WriteLine($"Scores      : max {rating.HighScore} | moy {rating.AverageScore:F1}");
                global::System.Console.WriteLine($"Derniere MAJ: {rating.UpdatedAt:O}");
                break;
        }

        _logger.LogInformation("Rating consulté: {PlayerId} ({Format})", playerId, context.OutputFormat);
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

