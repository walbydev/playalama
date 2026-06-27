using System.Globalization;
using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

/// <summary>
/// Gère le niveau de zoom du plateau et le persiste en localStorage.
/// Scoped per-circuit (Blazor Server).
/// </summary>
public sealed class BoardZoomService(IJSRuntime js)
{
    private static readonly int[] AllowedZoomPercents = [100, 125, 150];

    private int _zoomPercent = 100;
    private bool _initialized;

    public int ZoomPercent => _zoomPercent;
    public string ZoomFactorCss => (_zoomPercent / 100d).ToString("0.##", CultureInfo.InvariantCulture);

    public event Action? ZoomChanged;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var stored = await js.InvokeAsync<int?>("playalamaBoardZoom.getZoom");
            if (stored.HasValue)
                _zoomPercent = NormalizeZoom(stored.Value);

            _initialized = true;
        }
        catch
        {
            _zoomPercent = 100;
        }
    }

    public async Task SetAsync(int zoomPercent)
    {
        var normalized = NormalizeZoom(zoomPercent);
        if (_zoomPercent == normalized)
            return;

        _zoomPercent = normalized;

        try
        {
            await js.InvokeVoidAsync("playalamaBoardZoom.setZoom", _zoomPercent);
        }
        catch
        {
            // Prerendering : JS indisponible.
        }

        ZoomChanged?.Invoke();
    }

    private static int NormalizeZoom(int value)
    {
        foreach (var allowed in AllowedZoomPercents)
        {
            if (allowed == value) return allowed;
        }

        return 100;
    }
}
