namespace Lama.Core.Models;

/// <summary>
/// Tri souhaite pour les suggestions de coups.
/// </summary>
public enum MoveSuggestionSort
{
    Score = 0,
    Length = 1,
    Balanced = 2
}

/// <summary>
/// Requete pour recuperer des suggestions de coups.
/// </summary>
public sealed record SuggestMovesRequest(
    string GameId,
    string PlayerId,
    int Top = 2,
    MoveSuggestionSort Sort = MoveSuggestionSort.Score);

/// <summary>
/// Suggestion de coup prete a afficher dans la CLI/API.
/// </summary>
public sealed record SuggestedMoveCandidate(
    string Word,
    string Position,
    string Direction,
    int Score,
    int Length,
    double BalancedScore = 0);

/// <summary>
/// Reponse du cas d'usage de suggestion.
/// </summary>
public sealed record SuggestMovesResponse(
    IReadOnlyList<SuggestedMoveCandidate> Suggestions);

