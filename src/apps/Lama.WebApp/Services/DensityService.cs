using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

/// <summary>
/// Gère le mode de densité (normal/compact) et le persiste en localStorage.
/// Scoped per-circuit (Blazor Server).
/// </summary>
public sealed class DensityService(IJSRuntime js)
{
    private string _density = "normal";
    private bool _initialized;

    public const string Normal = "normal";
    public const string Compact = "compact";

    public string Density => _density;
    public bool IsCompact => _density == Compact;
    public bool IsInitialized => _initialized;

    public event Action? DensityChanged;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var stored = await js.InvokeAsync<string?>("playalamaDensity.getDensity");
            if (stored is "normal" or "compact")
                _density = stored;
            _initialized = true;
        }
        catch
        {
            _density = "normal";
        }
        DensityChanged?.Invoke();
    }

    public async Task SetAsync(string density)
    {
        _density = density is "compact" ? Compact : Normal;
        await ApplyAsync();
        DensityChanged?.Invoke();
    }

    public async Task ToggleAsync()
    {
        _density = _density == Compact ? Normal : Compact;
        await ApplyAsync();
        DensityChanged?.Invoke();
    }

    private async Task ApplyAsync()
    {
        try
        {
            await js.InvokeVoidAsync("playalamaDensity.setDensity", _density);
        }
        catch { /* prerendering */ }
    }
}
