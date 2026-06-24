using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

/// <summary>
/// Gère le thème dark/light et le persiste en localStorage.
/// Scoped per-circuit (Blazor Server).
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    private string _theme = "dark";
    private bool _initialized;

    public string Theme => _theme;
    public bool IsDark => _theme == "dark";

    public event Action? ThemeChanged;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var stored = await js.InvokeAsync<string?>("playalamaTheme.getTheme");
            if (stored is "dark" or "light")
                _theme = stored;
        }
        catch
        {
            _theme = "dark";
        }
    }

    public async Task ToggleAsync()
    {
        _theme = _theme == "dark" ? "light" : "dark";
        await ApplyAsync();
        ThemeChanged?.Invoke();
    }

    public async Task SetAsync(string theme)
    {
        _theme = theme is "dark" or "light" ? theme : "dark";
        await ApplyAsync();
        ThemeChanged?.Invoke();
    }

    private async Task ApplyAsync()
    {
        try
        {
            await js.InvokeVoidAsync("playalamaTheme.setTheme", _theme);
        }
        catch { /* prerendering */ }
    }
}
