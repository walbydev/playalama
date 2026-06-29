using System.Globalization;
using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

/// <summary>
/// Pilote la disposition proportionnelle du plateau : densité S/M/L (auto-fit dominant),
/// panneaux escamotables, plein écran. Persisté en localStorage. Scoped per-circuit.
/// </summary>
public sealed class GameLayoutService(IJSRuntime js)
{
    public const string Small = "s";
    public const string Medium = "m";
    public const string Large = "l";

    private string _density = Medium;
    private bool _fullscreen;
    private readonly HashSet<string> _collapsedPanels = new(StringComparer.Ordinal);
    private bool _initialized;

    public string Density => _density;
    public bool IsFullscreen => _fullscreen;

    /// <summary>Multiplicateur appliqué au clamp d'auto-fit (S/M/L).</summary>
    public string ScaleCss => _density switch
    {
        Small => "0.85",
        Large => "1.2",
        _ => "1",
    };

    public bool IsPanelCollapsed(string panel) => _collapsedPanels.Contains(panel);

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try
        {
            var state = await js.InvokeAsync<LayoutState?>("playalamaGameLayout.get");
            if (state is not null)
            {
                _density = Normalize(state.Density);
                _fullscreen = state.Fullscreen;
                _collapsedPanels.Clear();
                foreach (var p in state.Collapsed ?? [])
                    _collapsedPanels.Add(p);
            }
            _initialized = true;
        }
        catch
        {
            _density = Medium;
        }
    }

    public async Task SetDensityAsync(string density)
    {
        var d = Normalize(density);
        if (d == _density) return;
        _density = d;
        await PersistAsync();
        Changed?.Invoke();
    }

    public async Task ToggleFullscreenAsync()
    {
        _fullscreen = !_fullscreen;
        try { await js.InvokeVoidAsync("playalamaGameLayout.setFullscreen", _fullscreen); }
        catch { /* prerender */ }
        await PersistAsync();
        Changed?.Invoke();
    }

    public async Task TogglePanelAsync(string panel)
    {
        if (!_collapsedPanels.Remove(panel))
            _collapsedPanels.Add(panel);
        await PersistAsync();
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            await js.InvokeVoidAsync("playalamaGameLayout.set",
                new LayoutState(_density, _fullscreen, _collapsedPanels.ToArray()));
        }
        catch { /* prerender */ }
    }

    private static string Normalize(string? d) => d?.ToLowerInvariant() switch
    {
        Small => Small,
        Large => Large,
        _ => Medium,
    };

    public sealed record LayoutState(string Density, bool Fullscreen, string[]? Collapsed);
}
