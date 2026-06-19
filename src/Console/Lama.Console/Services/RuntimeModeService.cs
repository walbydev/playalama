namespace Lama.Console.Services;

/// <summary>
/// Résout le mode d'exécution runtime (local/online) et l'URL du serveur central.
/// </summary>
public sealed class RuntimeModeService
{
    private const string ModeEnvVar = "LAMA_RUNTIME_MODE";
    private const string ServerUrlEnvVar = "LAMA_SERVER_URL";

    /// <summary>
    /// Mode runtime courant. Défaut: local.
    /// </summary>
    public RuntimeExecutionMode Mode
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable(ModeEnvVar);
            if (string.Equals(raw, "online", StringComparison.OrdinalIgnoreCase))
                return RuntimeExecutionMode.Online;

            if (string.Equals(raw, "local", StringComparison.OrdinalIgnoreCase))
                return RuntimeExecutionMode.Local;

            // Sans override explicite, une URL persistée active automatiquement le mode online.
            return string.IsNullOrWhiteSpace(ServerBaseUrl)
                ? RuntimeExecutionMode.Local
                : RuntimeExecutionMode.Online;
        }
    }

    /// <summary>
    /// Indique si le mode online est activé.
    /// </summary>
    public bool IsOnline => Mode == RuntimeExecutionMode.Online;

    /// <summary>
    /// URL de base du serveur central.
    /// </summary>
    public string? ServerBaseUrl =>
        Environment.GetEnvironmentVariable(ServerUrlEnvVar)
        ?? RuntimeServerConfigStore.LoadServerUrl();
}

public enum RuntimeExecutionMode
{
    Local = 0,
    Online = 1
}

