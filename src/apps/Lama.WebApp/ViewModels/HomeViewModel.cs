using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

public sealed class HomeViewModel(LamaApiClient api)
{
    // ── Classements preview ───────────────────────────────────────────────────

    public string SelectedTab { get; private set; } = "tournament";
    public IReadOnlyList<LeaderboardEntry> PreviewEntries { get; private set; } = [];
    public bool IsLoadingLeaderboard { get; private set; }

    public IReadOnlyList<GameModeViewModel> Modes => GameModes.All;

    // ── Arguments "Pourquoi PLAYLAMA?" ───────────────────────────────────────

    public IReadOnlyList<WhyArgument> WhyArguments { get; } =
    [
        new("🌐", "100 % Web & CLI",  "Joue depuis ton navigateur ou ton terminal sans rien installer."),
        new("⚙️", "Open Source",      "Code source disponible sur GitHub. Contribue, fork, adapte."),
        new("🔒", "Sans tracking",    "Aucune publicité, aucun cookie tiers. Ta vie privée est sacrée."),
        new("🌍", "6 langues",        "Français, Anglais, Espagnol, Allemand, Italien, Portugais."),
        new("📱", "Responsive",       "Optimisé mobile, tablette et desktop avec thème sombre et clair."),
        new("🏆", "Classements ELO",  "Système ELO par mode de jeu. Grimpe et deviens légende."),
    ];

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadLeaderboardAsync(SelectedTab, ct);
    }

    public async Task SwitchTabAsync(string tab, CancellationToken ct = default)
    {
        if (SelectedTab == tab) return;
        SelectedTab = tab;
        await LoadLeaderboardAsync(tab, ct);
    }

    private async Task LoadLeaderboardAsync(string mode, CancellationToken ct)
    {
        IsLoadingLeaderboard = true;
        try
        {
            PreviewEntries = await api.GetLeaderboardAsync(mode, limit: 5, cancellationToken: ct);
        }
        catch
        {
            PreviewEntries = [];
        }
        finally
        {
            IsLoadingLeaderboard = false;
        }
    }
}

public sealed record WhyArgument(string Icon, string Title, string Text);
