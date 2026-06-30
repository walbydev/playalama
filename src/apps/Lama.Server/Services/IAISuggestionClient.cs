using Lama.Contracts;

namespace Lama.Server.Services;

/// <summary>
/// Client vers le service Lama.AIServer pour les suggestions de coups.
/// </summary>
public interface IAISuggestionClient
{
    /// <summary>
    /// Demande des suggestions de coups au service IA.
    /// Retourne une liste vide en cas d'erreur ou d'indisponibilité — jamais d'exception.
    /// </summary>
    Task<IReadOnlyList<AISuggestion>> SuggestAsync(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        int timeoutSeconds,
        string languageCode,
        CancellationToken ct);
}

/// <summary>
/// Suggestion retournée par le service IA.
/// </summary>
public record AISuggestion(
    string Word,
    int Score,
    int Length,
    int StartRow,
    int StartCol,
    bool IsHorizontal,
    string Category);
