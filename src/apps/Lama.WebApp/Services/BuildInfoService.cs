using System.Text.Json;

namespace Lama.WebApp.Services;

/// <summary>
/// Service pour charger et gérer les infos de build (version, build number, timestamp).
/// Lit depuis wwwroot/build-info.json généré à la compilation.
/// </summary>
public class BuildInfoService
{
    private BuildInfo _buildInfo = new() { Version = "–", BuildNumber = 0, BuildTimestamp = DateTime.MinValue };

    public BuildInfo BuildInfo => _buildInfo;

    public async Task InitializeAsync(HttpClient httpClient)
    {
        try
        {
            var response = await httpClient.GetAsync("build-info.json");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var version = root.TryGetProperty("version", out var vProp) ? vProp.GetString() : "–";
                var buildNumber = root.TryGetProperty("buildNumber", out var bProp) ? bProp.GetInt32() : 0;
                var timestamp = root.TryGetProperty("buildTimestamp", out var tProp)
                    ? DateTime.TryParse(tProp.GetString(), out var dt) ? dt : DateTime.MinValue
                    : DateTime.MinValue;

                _buildInfo = new() { Version = version ?? "–", BuildNumber = buildNumber, BuildTimestamp = timestamp };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠ BuildInfoService: {ex.Message}");
        }
    }
}

public record BuildInfo
{
    public string Version { get; set; } = "–";
    public int BuildNumber { get; set; }
    public DateTime BuildTimestamp { get; set; } = DateTime.MinValue;
}
