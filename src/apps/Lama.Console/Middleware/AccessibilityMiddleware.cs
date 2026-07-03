using Spectre.Console;

namespace Lama.Console.Commands.Middleware;

/// <summary>
/// Middleware d'accessibilité pour la CLI.
/// Gère le mode haut contraste, les couleurs, et les options d'affichage.
/// </summary>
public class AccessibilityMiddleware
{
    private static bool _highContrastMode;
    private static bool _noColorMode;
    private static int _fontSizeScale = 100; // 100, 125, 150, 200

    /// <summary>
    /// Active le mode haut contraste.
    /// </summary>
    public static void EnableHighContrast()
    {
        _highContrastMode = true;
        // En mode haut contraste, on utilise des couleurs plus contrastées
        AnsiConsole.Profile.Capabilities.Ansi = true;
    }

    /// <summary>
    /// Désactive le mode haut contraste.
    /// </summary>
    public static void DisableHighContrast()
    {
        _highContrastMode = false;
        AnsiConsole.Profile.Capabilities.Ansi = true;
    }

    /// <summary>
    /// Active le mode sans couleur (pour terminaux basiques).
    /// </summary>
    public static void EnableNoColor()
    {
        _noColorMode = true;
        AnsiConsole.Profile.Capabilities.Ansi = false;
    }

    /// <summary>
    /// Désactive le mode sans couleur.
    /// </summary>
    public static void DisableNoColor()
    {
        _noColorMode = false;
        AnsiConsole.Profile.Capabilities.Ansi = true;
    }

    /// <summary>
    /// Définit l'échelle de taille de police (pour référence future).
    /// </summary>
    public static void SetFontSizeScale(int scale)
    {
        _fontSizeScale = scale;
    }

    /// <summary>
    /// Applique les paramètres d'accessibilité depuis les variables d'environnement.
    /// </summary>
    public static void ApplyFromEnvironment()
    {
        var highContrast = Environment.GetEnvironmentVariable("LAMA_HIGH_CONTRAST");
        if (highContrast == "1" || highContrast?.ToLower() == "true")
        {
            EnableHighContrast();
        }

        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        if (noColor == "1" || noColor?.ToLower() == "true")
        {
            EnableNoColor();
        }

        var fontSize = Environment.GetEnvironmentVariable("LAMA_FONT_SIZE");
        if (int.TryParse(fontSize, out var scale))
        {
            SetFontSizeScale(scale);
        }
    }

    /// <summary>
    /// Vérifie si le mode haut contraste est activé.
    /// </summary>
    public static bool IsHighContrastEnabled => _highContrastMode;

    /// <summary>
    /// Vérifie si le mode sans couleur est activé.
    /// </summary>
    public static bool IsNoColorEnabled => _noColorMode;

    /// <summary>
    /// Obtient l'échelle de taille de police actuelle.
    /// </summary>
    public static int FontSizeScale => _fontSizeScale;

    /// <summary>
    /// Rendu d'un texte avec contraste élevé si activé.
    /// </summary>
    public static void WriteHighContrast(string text)
    {
        if (_highContrastMode)
        {
            AnsiConsole.Markup($"[bold white]{text}[/]");
        }
        else
        {
            AnsiConsole.Write(text);
        }
    }

    /// <summary>
    /// Rendu d'un texte avec contraste élevé si activé, suivi d'un newline.
    /// </summary>
    public static void WriteLineHighContrast(string text)
    {
        if (_highContrastMode)
        {
            AnsiConsole.MarkupLine($"[bold white]{text}[/]");
        }
        else
        {
            AnsiConsole.WriteLine(text);
        }
    }
}
