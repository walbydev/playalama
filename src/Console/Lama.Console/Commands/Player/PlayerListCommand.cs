using System.Text.Json;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Player;

/// <summary>
/// Commande <c>lama player list</c>.
/// Affiche les profils joueurs persistants.
/// </summary>
public sealed class PlayerListCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "player.list";

    private readonly IPlayerProfileService _playerProfileService;
    private readonly IPlayerRatingService _playerRatingService;
    private readonly ILogger<PlayerListCommand> _logger;

    public PlayerListCommand(
        IPlayerProfileService playerProfileService,
        IPlayerRatingService playerRatingService,
        ILogger<PlayerListCommand> logger)
    {
        _playerProfileService = playerProfileService;
        _playerRatingService = playerRatingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var countryFilter = context.GetOption("country");
        var regionFilter = context.GetOption("region");

        var profiles = await _playerProfileService.ListAsync();

        var filtered = profiles
            .Where(p => string.IsNullOrWhiteSpace(countryFilter) ||
                        string.Equals(p.Country, countryFilter, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(regionFilter) ||
                        string.Equals(p.Region, regionFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var entries = new List<PlayerListEntry>(filtered.Count);
        foreach (var profile in filtered)
        {
            var rating = await _playerRatingService.GetRatingAsync(profile.PlayerId);
            entries.Add(new PlayerListEntry(
                profile.PlayerId,
                profile.DisplayName,
                profile.Pseudo,
                profile.PublicName,
                profile.Country,
                profile.Region,
                profile.BirthYear,
                rating.EloRating,
                rating.Level,
                rating.LevelName,
                rating.WinsCount,
                rating.LossesCount,
                rating.AbandonedCount,
                rating.WinRate));
        }

        switch (context.OutputFormat.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(entries, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                break;

            case "csv":
                global::System.Console.WriteLine(
                    "playerId,displayName,pseudo,publicName,country,region,birthYear,elo,level,levelName,wins,losses,abandoned,winRate");
                foreach (var item in entries)
                {
                    global::System.Console.WriteLine(
                        $"{item.PlayerId},{Escape(item.DisplayName)},{Escape(item.Pseudo)},{Escape(item.PublicName)},{Escape(item.Country)},{Escape(item.Region)},{item.BirthYear},{item.Elo:F0},{item.Level},{Escape(item.LevelName)},{item.WinsCount},{item.LossesCount},{item.AbandonedCount},{item.WinRate:F2}");
                }
                break;

            default:
                global::System.Console.WriteLine("=== JOUEURS ===");
                if (filtered.Count == 0)
                {
                    global::System.Console.WriteLine("Aucun profil joueur.");
                    break;
                }

                global::System.Console.WriteLine($"Total: {filtered.Count}");
                global::System.Console.WriteLine();

                foreach (var item in entries)
                {
                    global::System.Console.WriteLine(
                        $"- {item.PublicName} ({item.PlayerId}) | {item.Elo:F0} Elo | {item.LevelName}");
                    global::System.Console.WriteLine(
                        $"  Nom: {item.DisplayName} | Pseudo: {(item.Pseudo ?? "-")} | Pays/Region: {(item.Country ?? "-")}/{(item.Region ?? "-")}");
                    global::System.Console.WriteLine(
                        $"  Bilan: {item.WinsCount}V/{item.LossesCount}D/{item.AbandonedCount}A (WR {item.WinRate:F1}%)");
                }
                break;
        }

        _logger.LogInformation(
            "Liste joueurs consultée: {Count} entrées (country={Country}, region={Region}, format={Format})",
            filtered.Count,
            countryFilter ?? "*",
            regionFilter ?? "*",
            context.OutputFormat);

        return ExitCodes.Success;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private sealed record PlayerListEntry(
        string PlayerId,
        string DisplayName,
        string? Pseudo,
        string PublicName,
        string? Country,
        string? Region,
        int? BirthYear,
        double Elo,
        int Level,
        string LevelName,
        int WinsCount,
        int LossesCount,
        int AbandonedCount,
        double WinRate);
}

