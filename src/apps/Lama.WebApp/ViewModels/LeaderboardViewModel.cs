using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

public sealed class LeaderboardViewModel(LamaApiClient api)
{
    public string SelectedMode    { get; private set; } = "tournament";
    public string SelectedCountry { get; private set; } = "";
    public IReadOnlyList<LeaderboardEntry> Entries { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    public static readonly IReadOnlyList<(string Code, string Label)> Countries =
    [
        ("",   "🌍 Tous les pays"),
        ("FR", "🇫🇷 France"),
        ("BE", "🇧🇪 Belgique"),
        ("CH", "🇨🇭 Suisse"),
        ("CA", "🇨🇦 Canada"),
        ("DE", "🇩🇪 Allemagne"),
        ("ES", "🇪🇸 Espagne"),
        ("GB", "🇬🇧 Royaume-Uni"),
        ("US", "🇺🇸 États-Unis"),
    ];

    public static readonly IReadOnlyList<(string Mode, string Label)> Modes =
    [
        ("tournament", "🏆 Tournois"),
        ("free",       "⚔️ Parties libres"),
        ("blitz",      "⚡ Blitz"),
    ];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var country = string.IsNullOrWhiteSpace(SelectedCountry) ? null : SelectedCountry;
            Entries = await api.GetLeaderboardAsync(SelectedMode, country, limit: 100, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Entries = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task SwitchModeAsync(string mode, CancellationToken ct = default)
    {
        SelectedMode = mode;
        return LoadAsync(ct);
    }

    public Task FilterByCountryAsync(string countryCode, CancellationToken ct = default)
    {
        SelectedCountry = countryCode;
        return LoadAsync(ct);
    }
}
