using System.Text.Json;
using System.Text.Json.Serialization;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Session;

/// <summary>
/// Implémentation de <see cref="ISessionService"/> basée sur un fichier JSON local.
///
/// Le fichier est stocké dans le répertoire de configuration utilisateur,
/// résolu de façon cross-platform via <see cref="Environment.SpecialFolder.ApplicationData"/> :
/// <list type="bullet">
///   <item>Linux   : <c>~/.config/lama/session.json</c></item>
///   <item>Windows : <c>%APPDATA%\lama\session.json</c></item>
///   <item>macOS   : <c>~/Library/Application Support/lama/session.json</c></item>
/// </list>
///
/// Le chemin peut être surchargé via la variable d'environnement
/// <c>LAMA_SESSION_DIR</c> (utile pour les tests automatisés et la CI).
/// </summary>
public sealed class SessionService : ISessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ILogger<SessionService> _logger;

    /// <inheritdoc />
    public string SessionFilePath { get; }

    /// <summary>
    /// Initialise le service de session.
    /// </summary>
    /// <param name="logger">Logger injecté.</param>
    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
        SessionFilePath = ResolveSessionFilePath();
    }

    /// <inheritdoc />
    public SessionContext? LoadSession()
    {
        if (!File.Exists(SessionFilePath))
        {
            _logger.LogDebug("Aucun fichier de session trouvé : {Path}", SessionFilePath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(SessionFilePath);
            var session = JsonSerializer.Deserialize<SessionContext>(json, JsonOptions);

            if (session is null)
            {
                _logger.LogWarning("Fichier de session vide ou invalide : {Path}", SessionFilePath);
                return null;
            }

            _logger.LogDebug(
                "Session chargée : GameId={GameId} Player={PlayerName} Role={Role} Level={Level}",
                session.GameId, session.PlayerName, session.Role, session.GameLevel);

            return session;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Fichier de session corrompu, ignoré : {Path}", SessionFilePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex,
                "Impossible de lire le fichier de session : {Path}", SessionFilePath);
            return null;
        }
    }

    /// <inheritdoc />
    public void SaveSession(SessionContext session)
    {
        try
        {
            var dir = Path.GetDirectoryName(SessionFilePath)!;
            Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(session, JsonOptions);
            File.WriteAllText(SessionFilePath, json);

            _logger.LogDebug(
                "Session sauvegardée : GameId={GameId} Player={PlayerName}",
                session.GameId, session.PlayerName);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex,
                "Impossible d'écrire le fichier de session : {Path}", SessionFilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public void ClearSession()
    {
        if (!File.Exists(SessionFilePath))
            return;

        try
        {
            File.Delete(SessionFilePath);
            _logger.LogDebug("Session supprimée : {Path}", SessionFilePath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex,
                "Impossible de supprimer le fichier de session : {Path}", SessionFilePath);
        }
    }

    /// <summary>
    /// Résout le chemin du fichier de session de façon cross-platform.
    /// Priorité : variable d'environnement LAMA_SESSION_DIR > ApplicationData.
    /// </summary>
    private static string ResolveSessionFilePath()
    {
        // Surcharge via variable d'environnement (tests automatisés, CI)
        var envDir = Environment.GetEnvironmentVariable("LAMA_SESSION_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
            return Path.Combine(envDir, "session.json");

        // Chemin standard cross-platform
        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(appData, "lama", "session.json");
    }
}
