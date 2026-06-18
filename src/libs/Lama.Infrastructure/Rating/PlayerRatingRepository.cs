using System.Text.Json;
using System.Text.Json.Serialization;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Rating;

/// <summary>
/// Référentiel pour persister les ratings des joueurs au format JSON.
/// Tous les ratings sont stockés dans un seul fichier : {ratingsDir}/player-ratings.json
/// </summary>
public sealed class PlayerRatingRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _ratingsFile;
    private readonly ILogger<PlayerRatingRepository> _logger;
    private Dictionary<string, PlayerRating>? _cache;

    public PlayerRatingRepository(ILogger<PlayerRatingRepository> logger)
    {
        _logger = logger;
        _ratingsFile = ResolveRatingsFile();
    }

    /// <summary>
    /// Obtient le rating d'un joueur. Retourne un rating par défaut s'il n'existe pas.
    /// </summary>
    public PlayerRating GetRating(string playerId)
    {
        var ratings = LoadAllRatings();

        if (ratings.TryGetValue(playerId, out var rating))
            return rating;

        // Créer un rating par défaut
        return new PlayerRating(
            PlayerId: playerId,
            EloRating: 1200,
            Level: 1,
            LevelName: "🌱 Jeune Lama",
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Sauvegarde les ratings mis à jour.
    /// </summary>
    public void SaveRatings(IEnumerable<PlayerRating> ratings)
    {
        var ratingsList = ratings.ToList();
        if (ratingsList.Count == 0)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(_ratingsFile)!);

        var all = LoadAllRatings();

        foreach (var rating in ratingsList)
        {
            all[rating.PlayerId] = rating;
        }

        var json = JsonSerializer.Serialize(all.Values.ToList(), JsonOptions);
        File.WriteAllText(_ratingsFile, json);

        _cache = all;

        _logger.LogDebug("Ratings sauvegardés : {Count} joueurs", ratingsList.Count);
    }

    /// <summary>
    /// Charge tous les ratings.
    /// </summary>
    public Dictionary<string, PlayerRating> LoadAllRatings()
    {
        if (_cache is not null)
            return new Dictionary<string, PlayerRating>(_cache);

        if (!File.Exists(_ratingsFile))
            return new Dictionary<string, PlayerRating>();

        try
        {
            var json = File.ReadAllText(_ratingsFile);
            var ratings = JsonSerializer.Deserialize<List<PlayerRating>>(json, JsonOptions) ?? new();

            // Migration douce: anciens fichiers n'avaient pas EloOpen/EloTournament.
            ratings = ratings
                .Select(r => r with
                {
                    EloOpen = r.EloOpen > 0 ? r.EloOpen : (r.EloRating > 0 ? r.EloRating : 1200),
                    EloTournament = r.EloTournament > 0 ? r.EloTournament : 1200,
                    EloRating = r.EloRating > 0 ? r.EloRating : (r.EloOpen > 0 ? r.EloOpen : 1200)
                })
                .ToList();

            _cache = ratings.ToDictionary(r => r.PlayerId);
            return new Dictionary<string, PlayerRating>(_cache);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Erreur desérialisation ratings : {Exception}", ex.Message);
            return new Dictionary<string, PlayerRating>();
        }
    }

    /// <summary>
    /// Obtient les ratings des joueurs spécifiés.
    /// </summary>
    public Dictionary<string, PlayerRating> GetRatings(IReadOnlyList<string> playerIds)
    {
        var all = LoadAllRatings();
        var result = new Dictionary<string, PlayerRating>();

        foreach (var playerId in playerIds)
        {
            result[playerId] = all.TryGetValue(playerId, out var r)
                ? r
                : GetRating(playerId);
        }

        return result;
    }

    /// <summary>
    /// Obtient le top N des joueurs par Elo.
    /// </summary>
    public List<PlayerRating> GetLeaderboard(int topCount = 100)
    {
        var all = LoadAllRatings();
        return all.Values
            .OrderByDescending(r => r.EloRating)
            .Take(topCount)
            .ToList();
    }

    /// <summary>
    /// Obtient les joueurs d'un niveau donné.
    /// </summary>
    public List<PlayerRating> GetPlayersByLevel(int level)
    {
        var all = LoadAllRatings();
        return all.Values
            .Where(r => r.Level == level)
            .OrderByDescending(r => r.EloRating)
            .ToList();
    }

    /// <summary>
    /// Efface tous les ratings (admin only).
    /// </summary>
    public void ClearAll()
    {
        if (File.Exists(_ratingsFile))
            File.Delete(_ratingsFile);

        _cache = null;

        _logger.LogInformation("Tous les ratings ont été supprimés");
    }

    /// <summary>
    /// Invalide le cache (force rechargement depuis disque).
    /// </summary>
    public void InvalidateCache()
    {
        _cache = null;
    }

    private static string ResolveRatingsFile()
    {
        var baseDir = Environment.GetEnvironmentVariable("LAMA_SESSION_DIR") 
            ?? ResolveBaseDirectory();

        return Path.Combine(baseDir, "ratings", "player-ratings.json");
    }

    private static string ResolveBaseDirectory()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix when Environment.GetEnvironmentVariable("HOME") is not null =>
                Path.Combine(Environment.GetEnvironmentVariable("HOME")!, ".config", "lama"),
            PlatformID.Win32NT =>
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "lama"),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "lama")
        };
    }
}

