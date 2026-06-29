using System.Globalization;
using Microsoft.JSInterop;

namespace Lama.WebApp.Services;

/// <summary>
/// Gère la langue de l'interface (FR/EN/DE) et la persiste via cookie .AspNetCore.Culture.
/// Scoped per-circuit (Blazor Server).
/// </summary>
public sealed class LanguageService(IJSRuntime js)
{
    public static readonly IReadOnlyList<(string Code, string Label)> Available = new[]
    {
        ("fr", "Français"), ("en", "English"), ("de", "Deutsch")
    };

    public string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>Change la langue : écrit le cookie et recharge la page.</summary>
    public async Task SetAsync(string code)
    {
        var normalized = Available.Any(l => l.Code == code) ? code : "fr";
        if (normalized == Current) return;
        await js.InvokeVoidAsync("playalamaLang.set", normalized);
    }
}
