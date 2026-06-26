namespace Lama.AIServer.Models;

/// <summary>
/// Réponse du service de suggestion.
/// </summary>
public record SuggestResponse(
    IReadOnlyList<SuggestionDto> Suggestions,
    string Message,
    string Language);

/// <summary>
/// Une suggestion de coup retournée au client.
/// </summary>
public record SuggestionDto(
    string Word,
    int Score,
    int Length,
    int StartRow,
    int StartCol,
    bool IsHorizontal,
    string Category);
