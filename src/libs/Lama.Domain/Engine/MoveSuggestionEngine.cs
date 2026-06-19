using Lama.Contracts;
using Lama.Domain.Scoring;

namespace Lama.Domain.Engine;

/// <summary>
/// Moteur de suggestion de coups (stub).
/// Le moteur complet sera ajoute dans une prochaine iteration.
/// </summary>
public sealed class MoveSuggestionEngine
{
    private readonly IReadOnlySet<string> _dictionary;
    private readonly ScoreCalculator _scoreCalculator;

    public MoveSuggestionEngine()
        : this(new HashSet<string>(), new Dictionary<char, int>())
    {
    }

    public MoveSuggestionEngine(
        IReadOnlySet<string> dictionary,
        IReadOnlyDictionary<char, int> letterScores)
    {
        _dictionary = dictionary;
        _scoreCalculator = new ScoreCalculator(letterScores);
    }

    /// <summary>
    /// Propose les meilleurs coups pour le joueur courant.
    /// </summary>
    public IReadOnlyList<MoveSuggestion> SuggestTopMoves(
        GameState gameState,
        Player currentPlayer,
        int top,
        MoveSuggestionStrategy strategy)
    {
        if (top <= 0 || _dictionary.Count == 0)
            return [];

        // V1: ne traite que le premier coup (plateau vide). Les autres cas reviendront ensuite.
        if (HasAnyTile(gameState.Board))
            return [];

        var rack = currentPlayer.Rack.Select(char.ToUpperInvariant).ToList();
        var candidates = new List<MoveSuggestion>();

        foreach (var rawWord in _dictionary)
        {
            var word = rawWord.Trim().ToUpperInvariant();
            if (word.Length < 2 || word.Length > rack.Count)
                continue;

            if (!TryAssignWildcards(word, rack, out var wildcardIndices))
                continue;

            AddCandidates(candidates, gameState.Board, word, wildcardIndices, isHorizontal: true);
            AddCandidates(candidates, gameState.Board, word, wildcardIndices, isHorizontal: false);
        }

        var ordered = strategy switch
        {
            MoveSuggestionStrategy.Length => candidates
                .OrderByDescending(c => c.Length)
                .ThenByDescending(c => c.Score)
                .ThenBy(c => c.Word, StringComparer.Ordinal),

            MoveSuggestionStrategy.Balanced => candidates
                .OrderByDescending(c => c.HeuristicScore)
                .ThenByDescending(c => c.Score)
                .ThenBy(c => c.Word, StringComparer.Ordinal),

            _ => candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Length)
                .ThenBy(c => c.Word, StringComparer.Ordinal)
        };

        return ordered.Take(top).ToList();
    }

    private void AddCandidates(
        List<MoveSuggestion> candidates,
        BoardState board,
        string word,
        ISet<int> wildcardIndices,
        bool isHorizontal)
    {
        const int center = 7;
        var minStart = Math.Max(0, center - word.Length + 1);
        var maxStart = Math.Min(center, 14 - word.Length + 1);

        for (var start = minStart; start <= maxStart; start++)
        {
            var placements = new Dictionary<Position, char>(word.Length);
            var wildcardPositions = new HashSet<Position>();

            for (var i = 0; i < word.Length; i++)
            {
                var pos = isHorizontal
                    ? new Position(center, start + i)
                    : new Position(start + i, center);

                placements[pos] = word[i];
                if (wildcardIndices.Contains(i))
                    wildcardPositions.Add(pos);
            }

            var score = _scoreCalculator.Calculate(placements, board, wildcardPositions);

            // Heuristique V1: equivalent au score immediat.
            var heuristic = score;

            candidates.Add(new MoveSuggestion(
                Word: word,
                Placements: placements,
                Score: score,
                Length: word.Length,
                HeuristicScore: heuristic));
        }
    }

    private static bool TryAssignWildcards(string word, IReadOnlyList<char> rack, out ISet<int> wildcardIndices)
    {
        wildcardIndices = new HashSet<int>();

        var available = rack
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());

        available.TryGetValue('*', out var wildcards);

        for (var i = 0; i < word.Length; i++)
        {
            var letter = word[i];
            if (available.TryGetValue(letter, out var count) && count > 0)
            {
                available[letter] = count - 1;
                continue;
            }

            if (wildcards <= 0)
                return false;

            wildcards--;
            wildcardIndices.Add(i);
        }

        return true;
    }

    private static bool HasAnyTile(BoardState board)
    {
        for (var row = 0; row < 15; row++)
            for (var col = 0; col < 15; col++)
                if (board.Grid[row, col] is not null)
                    return true;

        return false;
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

