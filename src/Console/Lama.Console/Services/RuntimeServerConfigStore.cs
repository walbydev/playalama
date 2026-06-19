using System.Text.Json;

namespace Lama.Console.Services;

/// <summary>
/// Persiste la configuration runtime locale de la CLI (URL serveur online).
/// </summary>
public static class RuntimeServerConfigStore
{
    private const string SessionDirEnvVar = "LAMA_SESSION_DIR";
    private const string ConfigFileName = "runtime.json";

    public static string? LoadServerUrl()
    {
        var path = ResolveConfigFilePath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<RuntimeServerConfig>(json);
            return string.IsNullOrWhiteSpace(config?.ServerUrl)
                ? null
                : config.ServerUrl;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void SaveServerUrl(string serverUrl)
    {
        var path = ResolveConfigFilePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var payload = new RuntimeServerConfig(serverUrl);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static bool ClearServerUrl()
    {
        var path = ResolveConfigFilePath();
        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string ResolveConfigFilePath()
    {
        var envDir = Environment.GetEnvironmentVariable(SessionDirEnvVar);
        if (!string.IsNullOrWhiteSpace(envDir))
            return Path.Combine(envDir, ConfigFileName);

        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(appData, "lama", ConfigFileName);
    }

    private sealed record RuntimeServerConfig(string ServerUrl);
}

