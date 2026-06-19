using Lama.Contracts;

namespace Lama.Domain.Engine;

/// <summary>
/// Moteur de suggestion de coups (stub).
/// Le moteur complet sera ajoute dans une prochaine iteration.
/// </summary>
public sealed class MoveSuggestionEngine
{
    /// <summary>
    /// Propose les meilleurs coups pour le joueur courant.
    /// </summary>
    public IReadOnlyList<MoveSuggestion> SuggestTopMoves(
        GameState gameState,
        Player currentPlayer,
        int top,
        MoveSuggestionStrategy strategy)
    {
        _ = gameState;
        _ = currentPlayer;
        _ = top;
        _ = strategy;

        // Stub intentionnel: aucune proposition tant que l'algorithme n'est pas implemente.
        return [];
    }
}

/// <summary>
/// Strategie de classement des suggestions.
/// </summary>
public enum MoveSuggestionStrategy
{
    Score = 0,
    Length = 1,
    Balanced = 2
}

/// <summary>
/// Representation interne d'une suggestion calculee par le domaine.
/// </summary>
public sealed record MoveSuggestion(
    string Word,
    Dictionary<Position, char> Placements,
    int Score,
    int Length,
    double HeuristicScore = 0);

