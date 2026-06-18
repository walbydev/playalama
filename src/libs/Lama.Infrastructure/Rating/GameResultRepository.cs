using System.Text.Json;
using System.Text.Json.Serialization;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Rating;

/// <summary>
/// Référentiel pour persister les résultats de parties au format JSON.
/// Chaque résultat est stocké dans un fichier : {ratingsDir}/game-results/{gameId}.json
/// </summary>
public sealed class GameResultRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _resultsDirectory;
    private readonly ILogger<GameResultRepository> _logger;

    public GameResultRepository(ILogger<GameResultRepository> logger)
    {
        _logger = logger;
        _resultsDirectory = ResolveResultsDirectory();
    }

    /// <summary>
    /// Sauvegarde les résultats d'une partie.
    /// </summary>
    public void SaveGameResults(IReadOnlyList<GameResult> results)
    {
        if (results.Count == 0)
            return;

        Directory.CreateDirectory(_resultsDirectory);

        foreach (var result in results)
        {
            var filePath = GetResultFilePath(result.GameId, result.PlayerId);
            var json = JsonSerializer.Serialize(result, JsonOptions);
            File.WriteAllText(filePath, json);

            _logger.LogDebug("Résultat sauvegardé : {GameId} / {PlayerId}", result.GameId, result.PlayerId);
        }
    }

    /// <summary>
    /// Charge tous les résultats d'un joueur.
    /// </summary>
    public List<GameResult> LoadPlayerResults(string playerId)
    {
        var pattern = $"{playerId}-*.json";
        var results = new List<GameResult>();

        if (!Directory.Exists(_resultsDirectory))
            return results;

        var files = Directory.GetFiles(_resultsDirectory, pattern);

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var result = JsonSerializer.Deserialize<GameResult>(json, JsonOptions);

                if (result is not null)
                    results.Add(result);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Erreur desérialisation {File}: {Exception}", file, ex.Message);
            }
        }

        return results.OrderByDescending(r => r.PlayedAt).ToList();
    }

    /// <summary>
    /// Charge tous les résultats (pour recalcul global).
    /// </summary>
    public List<GameResult> LoadAllResults()
    {
        var results = new List<GameResult>();

        if (!Directory.Exists(_resultsDirectory))
            return results;

        var files = Directory.GetFiles(_resultsDirectory, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var result = JsonSerializer.Deserialize<GameResult>(json, JsonOptions);

                if (result is not null)
                    results.Add(result);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Erreur desérialisation {File}: {Exception}", file, ex.Message);
            }
        }

        return results;
    }

    /// <summary>
    /// Efface tous les résultats (admin only).
    /// </summary>
    public void ClearAllResults()
    {
        if (Directory.Exists(_resultsDirectory))
        {
            var files = Directory.GetFiles(_resultsDirectory, "*.json");
            foreach (var file in files)
            {
                File.Delete(file);
                _logger.LogDebug("Résultat supprimé : {File}", file);
            }
        }
    }

    private static string GetResultFilePath(string gameId, string playerId)
    {
        var fileName = $"{playerId}-{gameId}.json";
        var resultsDir = Path.Combine(ResolveResultsDirectory());
        return Path.Combine(resultsDir, fileName);
    }

    private static string ResolveResultsDirectory()
    {
        var baseDir = Environment.GetEnvironmentVariable("LAMA_SESSION_DIR") 
            ?? ResolveBaseDirectory();

        return Path.Combine(baseDir, "ratings", "game-results");
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

