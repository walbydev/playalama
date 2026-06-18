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
            return string.Equals(raw, "online", StringComparison.OrdinalIgnoreCase)
                ? RuntimeExecutionMode.Online
                : RuntimeExecutionMode.Local;
        }
    }

    /// <summary>
    /// Indique si le mode online est activé.
    /// </summary>
    public bool IsOnline => Mode == RuntimeExecutionMode.Online;

    /// <summary>
    /// URL de base du serveur central.
    /// </summary>
    public string ServerBaseUrl =>
        Environment.GetEnvironmentVariable(ServerUrlEnvVar)
        ?? "http://127.0.0.1:5055";
}

public enum RuntimeExecutionMode
{
    Local = 0,
    Online = 1
}

