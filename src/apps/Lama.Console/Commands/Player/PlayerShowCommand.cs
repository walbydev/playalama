using System.Text.Json;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Player;

/// <summary>
/// Commande <c>lama player show [playerId]</c>.
/// Affiche le profil complet d'un joueur (profil + rating + stats).
/// </summary>
public sealed class PlayerShowCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "player.show";

    private readonly IPlayerProfileService _playerProfileService;
    private readonly IPlayerRatingService _playerRatingService;
    private readonly ILogger<PlayerShowCommand> _logger;

    public PlayerShowCommand(
        IPlayerProfileService playerProfileService,
        IPlayerRatingService playerRatingService,
        ILogger<PlayerShowCommand> logger)
    {
        _playerProfileService = playerProfileService;
        _playerRatingService = playerRatingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerId = context.GetArgument(0) ?? context.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            global::System.Console.Error.WriteLine("[player show] PlayerId requis (argument ou session active).");
            return ExitCodes.InvalidArgument;
        }

        var profile = await _playerProfileService.GetByIdAsync(playerId);
        var rating = await _playerRatingService.GetRatingAsync(playerId);
        var stats = await _playerRatingService.GetStatisticsAsync(playerId);

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(new
                {
                    playerId,
                    displayName = profile?.DisplayName,
                    pseudo = profile?.Pseudo,
                    country = profile?.Country,
                    region = profile?.Region,
                    birthYear = profile?.BirthYear,
                    createdAt = profile?.CreatedAt,
                    updatedAt = profile?.UpdatedAt,
                    rating = new
                    {
                        rating.EloRating,
                        rating.Level,
                        rating.LevelName,
                        rating.WinsCount,
                        rating.LossesCount,
                        rating.AbandonedCount,
                        rating.WinRate
                    },
                    stats30d = stats.Last30Days
                }, new JsonSerializerOptions { WriteIndented = true }));
                break;

            case "csv":
                global::System.Console.WriteLine(
                    "playerId,displayName,pseudo,country,region,birthYear,elo,level,levelName,wins,losses,abandoned,winRate");
                global::System.Console.WriteLine(
                    $"{playerId},{Escape(profile?.DisplayName)},{Escape(profile?.Pseudo)},{Escape(profile?.Country)},{Escape(profile?.Region)},{profile?.BirthYear},{rating.EloRating:F0},{rating.Level},{Escape(rating.LevelName)},{rating.WinsCount},{rating.LossesCount},{rating.AbandonedCount},{rating.WinRate:F2}");
                break;

            default:
                global::System.Console.WriteLine("=== PROFIL JOUEUR ===");
                global::System.Console.WriteLine($"PlayerId : {playerId}");
                global::System.Console.WriteLine($"Nom      : {profile?.DisplayName ?? "(non renseigne)"}");
                global::System.Console.WriteLine($"Pseudo   : {profile?.Pseudo ?? "(non renseigne)"}");
                global::System.Console.WriteLine($"Pays     : {profile?.Country ?? "(non renseigne)"}");
                global::System.Console.WriteLine($"Region   : {profile?.Region ?? "(non renseigne)"}");
                global::System.Console.WriteLine($"Naissance: {(profile?.BirthYear is null ? "(non renseignee)" : profile.BirthYear)}");
                global::System.Console.WriteLine();
                global::System.Console.WriteLine("=== RATING ===");
                global::System.Console.WriteLine($"Niveau   : {rating.LevelName} (#{rating.Level})");
                global::System.Console.WriteLine($"Elo      : {rating.EloRating:F0}");
                global::System.Console.WriteLine($"Bilan    : {rating.WinsCount}V / {rating.LossesCount}D / {rating.AbandonedCount}A");
                global::System.Console.WriteLine($"WinRate  : {rating.WinRate:F1}%");
                global::System.Console.WriteLine();
                global::System.Console.WriteLine("=== STATS (30 jours) ===");
                global::System.Console.WriteLine($"Parties  : {stats.Last30Days.TotalGames}");
                global::System.Console.WriteLine($"Bilan    : {stats.Last30Days.Wins}V / {stats.Last30Days.Losses}D / {stats.Last30Days.Abandoned}A");
                global::System.Console.WriteLine($"WinRate  : {stats.Last30Days.WinRate:F1}%");
                break;
        }

        _logger.LogInformation("Profil consulté: {PlayerId}", playerId);
        return ExitCodes.Success;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}

