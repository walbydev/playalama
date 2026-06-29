namespace Lama.Contracts.Lexicon;

/// <summary>
/// Définition d'un mot (sens + nature grammaticale).
/// </summary>
public sealed record WordDefinition(int SenseIndex, string? PartOfSpeech, string Text);

/// <summary>
/// Informations enrichies sur un mot du lexique : définitions, synonymes, lien Wiktionnaire.
/// </summary>
public sealed record WordInfo(
    string Lemma,
    string LanguageCode,
    string? WiktionaryUrl,
    IReadOnlyList<WordDefinition> Definitions,
    IReadOnlyList<string> Synonyms);
