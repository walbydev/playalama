using System.Text.Json;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Profile;

/// <summary>
/// Persistance JSON des profils joueurs.
/// Fichier unique: {baseDir}/players/profiles.json
/// </summary>
public sealed class JsonPlayerProfileService : IPlayerProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _profilesFilePath;
    private readonly ILogger<JsonPlayerProfileService> _logger;
    private readonly object _sync = new();

    public JsonPlayerProfileService(ILogger<JsonPlayerProfileService> logger)
    {
        _logger = logger;
        _profilesFilePath = ResolveProfilesFilePath();
    }

    /// <inheritdoc />
    public Task<PlayerProfile> SaveAsync(PlayerProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.PlayerId))
            throw new ArgumentException("PlayerId requis.", nameof(profile));

        if (string.IsNullOrWhiteSpace(profile.DisplayName))
            throw new ArgumentException("DisplayName requis.", nameof(profile));

        ValidateBirthYear(profile.BirthYear);

        lock (_sync)
        {
            var all = LoadAllUnsafe();
            var now = DateTimeOffset.UtcNow;

            var createdAt = all.TryGetValue(profile.PlayerId, out var existing)
                ? existing.CreatedAt
                : (profile.CreatedAt == default ? now : profile.CreatedAt);

            var updated = profile with
            {
                DisplayName = profile.DisplayName.Trim(),
                Pseudo = Normalize(profile.Pseudo),
                Country = Normalize(profile.Country),
                Region = Normalize(profile.Region),
                CreatedAt = createdAt,
                UpdatedAt = now
            };

            all[updated.PlayerId] = updated;
            SaveAllUnsafe(all);

            _logger.LogInformation("Profil joueur sauvegardé: {PlayerId}", updated.PlayerId);
            return Task.FromResult(updated);
        }
    }

    /// <inheritdoc />
    public Task<PlayerProfile?> GetByIdAsync(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return Task.FromResult<PlayerProfile?>(null);

        lock (_sync)
        {
            var all = LoadAllUnsafe();
            return Task.FromResult(all.GetValueOrDefault(playerId.Trim()));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlayerProfile>> ListAsync()
    {
        lock (_sync)
        {
            var list = LoadAllUnsafe()
                .Values
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

            return Task.FromResult((IReadOnlyList<PlayerProfile>)list);
        }
    }

    private Dictionary<string, PlayerProfile> LoadAllUnsafe()
    {
        if (!File.Exists(_profilesFilePath))
            return new Dictionary<string, PlayerProfile>(StringComparer.Ordinal);

        try
        {
            var json = File.ReadAllText(_profilesFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, PlayerProfile>(StringComparer.Ordinal);

            var profiles = JsonSerializer.Deserialize<List<PlayerProfile>>(json, JsonOptions) ?? [];
            return profiles.ToDictionary(p => p.PlayerId, StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "profiles.json corrompu, fallback sur collection vide");
            return new Dictionary<string, PlayerProfile>(StringComparer.Ordinal);
        }
    }

    private void SaveAllUnsafe(Dictionary<string, PlayerProfile> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilesFilePath)!);

        var ordered = all.Values
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.PlayerId, StringComparer.Ordinal)
            .ToList();

        var json = JsonSerializer.Serialize(ordered, JsonOptions);
        File.WriteAllText(_profilesFilePath, json);
    }

    private static void ValidateBirthYear(int? birthYear)
    {
        if (birthYear is null)
            return;

        var currentYear = DateTime.UtcNow.Year;
        if (birthYear < 1900 || birthYear > currentYear)
            throw new ArgumentOutOfRangeException(nameof(birthYear),
                $"BirthYear doit être entre 1900 et {currentYear}.");
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static string ResolveProfilesFilePath()
    {
        var baseDir = Environment.GetEnvironmentVariable("LAMA_SESSION_DIR") ?? ResolveBaseDirectory();
        return Path.Combine(baseDir, "players", "profiles.json");
    }

    private static string ResolveBaseDirectory()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix when Environment.GetEnvironmentVariable("HOME") is not null =>
                Path.Combine(Environment.GetEnvironmentVariable("HOME")!, ".config", "lama"),
            PlatformID.Win32NT =>
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lama"),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lama")
        };
    }
}

