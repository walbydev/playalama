using System.Text.Json;
using System.Text.Json.Serialization;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Persistence;

/// <summary>
/// Implémentation de <see cref="IGameRepository"/> basée sur des fichiers JSON.
///
/// Chaque partie est stockée dans un fichier dédié :
/// <c>{gamesDir}/{gameId}.json</c>
///
/// Répertoire résolu de façon cross-platform :
/// <list type="bullet">
///   <item>Linux   : <c>~/.config/lama/games/</c></item>
///   <item>Windows : <c>%APPDATA%\lama\games\</c></item>
///   <item>macOS   : <c>~/Library/Application Support/lama/games/</c></item>
/// </list>
///
/// La variable d'environnement <c>LAMA_SESSION_DIR</c> surcharge le répertoire racine
/// (cohérent avec <c>SessionService</c> et <c>AccountService</c>).
///
/// Migration vers SQLite : implémenter <see cref="IGameRepository"/> avec
/// <c>SqliteGameRepository</c> — les use cases et les commandes ne changent pas.
/// </summary>
public sealed class JsonGameRepository : IGameRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private readonly string _gamesDirectory;
    private readonly ILogger<JsonGameRepository> _logger;

    /// <summary>Initialise le référentiel.</summary>
    public JsonGameRepository(ILogger<JsonGameRepository> logger)
    {
        _logger        = logger;
        _gamesDirectory = ResolveGamesDirectory();
    }

    /// <inheritdoc />
    public void Save(PersistedGame game)
    {
        Directory.CreateDirectory(_gamesDirectory);

        var filePath = GetFilePath(game.GameId);
        var json     = JsonSerializer.Serialize(game, JsonOptions);
        File.WriteAllText(filePath, json);

        _logger.LogDebug("Partie sauvegardée : {GameId} → {Path}", game.GameId, filePath);
    }

    /// <inheritdoc />
    public PersistedGame? Load(string gameId)
    {
        var filePath = GetFilePath(gameId);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("Partie introuvable : {GameId}", gameId);
            return null;
        }

        try
        {
            var json   = File.ReadAllText(filePath);
            var game   = JsonSerializer.Deserialize<PersistedGame>(json, JsonOptions);

            if (game is null)
            {
                _logger.LogWarning("Fichier de partie vide : {Path}", filePath);
                return null;
            }

            _logger.LogDebug("Partie chargée : {GameId}", gameId);
            return game;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Fichier de partie corrompu, ignoré : {Path}", filePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Impossible de lire la partie : {Path}", filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public void Delete(string gameId)
    {
        var filePath = GetFilePath(gameId);

        if (!File.Exists(filePath)) return;

        try
        {
            File.Delete(filePath);
            _logger.LogDebug("Partie supprimée : {GameId}", gameId);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Impossible de supprimer la partie : {Path}", filePath);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListGameIds()
    {
        if (!Directory.Exists(_gamesDirectory))
            return [];

        return Directory
            .GetFiles(_gamesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => id is not null)
            .Cast<string>()
            .OrderBy(id => id)
            .ToList();
    }

    /// <inheritdoc />
    public bool Exists(string gameId) => File.Exists(GetFilePath(gameId));

    // ── Helpers privés ────────────────────────────────────────────────────────

    private string GetFilePath(string gameId) =>
        Path.Combine(_gamesDirectory, $"{gameId}.json");

    private static string ResolveGamesDirectory()
    {
        var envDir = Environment.GetEnvironmentVariable("LAMA_SESSION_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
            return Path.Combine(envDir, "games");

        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(appData, "lama", "games");
    }
}
