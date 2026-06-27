using System.Globalization;
using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

/// <summary>
/// Gère le niveau de zoom du plateau et le persiste en localStorage.
/// Scoped per-circuit (Blazor Server).
/// </summary>
public sealed class BoardZoomService(IJSRuntime js)
{
    private static readonly int[] AllowedZoomPercents = [100, 125, 150, 200];

    private string _zoomMode = "100";
    private int _zoomPercent = 100;
    private bool _initialized;

    public int ZoomPercent => _zoomPercent;
    public bool IsAutoFit => string.Equals(_zoomMode, "auto", StringComparison.Ordinal);
    public string ZoomMode => _zoomMode;
    public string ZoomFactorCss => IsAutoFit
        ? "var(--board-auto-zoom, 1)"
        : (_zoomPercent / 100d).ToString("0.##", CultureInfo.InvariantCulture);

    public event Action? ZoomChanged;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var stored = await js.InvokeAsync<string?>("playalamaBoardZoom.getZoom");
            ApplyMode(stored);

            _initialized = true;
        }
        catch
        {
            _zoomMode = "100";
            _zoomPercent = 100;
        }
    }

    public async Task SetAsync(int zoomPercent)
    {
        await SetModeAsync(zoomPercent.ToString(CultureInfo.InvariantCulture));
    }

    public async Task SetModeAsync(string? mode)
    {
        var normalizedMode = NormalizeMode(mode);
        if (_zoomMode == normalizedMode)
            return;

        _zoomMode = normalizedMode;
        _zoomPercent = ParsePercentFromMode(_zoomMode);

        try
        {
            await js.InvokeVoidAsync("playalamaBoardZoom.setZoom", _zoomMode);
        }
        catch
        {
            // Prerendering : JS indisponible.
        }

        ZoomChanged?.Invoke();
    }

    private void ApplyMode(string? mode)
    {
        _zoomMode = NormalizeMode(mode);
        _zoomPercent = ParsePercentFromMode(_zoomMode);
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase))
            return "auto";

        if (int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return NormalizeZoom(parsed).ToString(CultureInfo.InvariantCulture);

        return "100";
    }

    private static int ParsePercentFromMode(string mode)
    {
        if (string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase))
            return 100;

        return int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? NormalizeZoom(parsed)
            : 100;
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
