namespace Lama.Contracts.Lexicon;

/// <summary>
/// Accès en lecture au lexique stocké en base (schéma lexicon).
/// </summary>
public interface ILexiconReader
{
    /// <summary>
    /// Charge l'ensemble des lemmes (en majuscules, A-Z) d'une langue.
    /// </summary>
    IReadOnlySet<string> LoadDictionary(string languageCode);

    /// <summary>
    /// Récupère les définitions/synonymes/URL d'un mot, ou null si absent.
    /// </summary>
    Task<WordInfo?> GetWordInfoAsync(string languageCode, string word, CancellationToken cancellationToken = default);
}
