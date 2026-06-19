namespace Lama.Contracts;

// <summary>
/// Abstraction pour la gestion des données linguistiques du jeu LAMA.
/// Permet le support multilingue (FR Phase 1-2, EN Phase 4+, etc.)
/// </summary>
public interface IGameLanguageProvider
{
    /// <summary>
    /// Obtient le dictionnaire des mots valides pour la langue.
    /// </summary>
    IReadOnlySet<string> GetDictionary();

    /// <summary>
    /// Obtient les points associés à chaque lettre.
    /// Exemple: 'A' -> 1 point, 'Z' -> 10 points
    /// </summary>
    IReadOnlyDictionary<char, int> GetLetterScores();

    /// <summary>
    /// Obtient la distribution des tuiles pour la langue.
    /// Exemple: 'E' -> 15 tuiles, '*' -> 2 jokers
    /// </summary>
    IReadOnlyDictionary<char, int> GetTileDistribution();

    /// <summary>
    /// Nom de la langue (ex: "Français", "English")
    /// </summary>
    string GetLanguageName();

    /// <summary>
    /// Code locale (ex: "fr-FR", "en-US")
    /// </summary>
    string GetLocale();
}