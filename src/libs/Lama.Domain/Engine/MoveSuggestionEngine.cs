using Lama.Contracts;
using Lama.Domain.Scoring;
using Lama.Domain.Validation;

namespace Lama.Domain.Engine;

/// <summary>
/// Moteur de suggestion de coups (stub).
/// Le moteur complet sera ajoute dans une prochaine iteration.
/// </summary>
public sealed class MoveSuggestionEngine
{
    private readonly IReadOnlySet<string> _dictionary;
    private readonly ScoreCalculator _scoreCalculator;
    private readonly MoveValidator _moveValidator;

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
        _moveValidator = new MoveValidator(dictionary);
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
        
        var rack = currentPlayer.Rack.Select(char.ToUpperInvariant).ToList();
        var isFirstMove = !HasAnyTile(gameState.Board);
        var candidates = new Dictionary<string, MoveSuggestion>(StringComparer.Ordinal);
        var boardAnchorLetters = GetBoardAnchorLetters(gameState.Board);

        foreach (var rawWord in _dictionary)
        {
            var word = rawWord.Trim().ToUpperInvariant();
            if (!IsAsciiWord(word) || word.Length < 2 || word.Length > 15)
                continue;

            if (isFirstMove)
            {
                if (word.Length > rack.Count)
                    continue;

                AddFirstMoveCandidates(candidates, gameState.Board, rack, word);
                continue;
            }

            if (!WordContainsAnyAnchorLetter(word, boardAnchorLetters))
                continue;

            AddConnectedCandidates(candidates, gameState.Board, rack, word);
        }

        var ordered = strategy switch
        {
            MoveSuggestionStrategy.Length => candidates.Values
                .OrderByDescending(c => c.Length)
                .ThenByDescending(c => c.Score)
                .ThenBy(c => c.Word, StringComparer.Ordinal),

            MoveSuggestionStrategy.Balanced => candidates.Values
                .OrderByDescending(c => c.HeuristicScore)
                .ThenByDescending(c => c.Score)
                .ThenBy(c => c.Word, StringComparer.Ordinal),

            _ => candidates.Values
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Length)
                .ThenBy(c => c.Word, StringComparer.Ordinal)
        };

        return ordered.Take(top).ToList();
    }

    private void AddFirstMoveCandidates(
        Dictionary<string, MoveSuggestion> candidates,
        BoardState board,
        IReadOnlyList<char> rack,
        string word,
        bool isHorizontal = true)
    {
        const int center = 7;
        for (var orientation = 0; orientation < 2; orientation++)
        {
            var horizontal = orientation == 0 ? isHorizontal : !isHorizontal;
            var minStart = Math.Max(0, center - word.Length + 1);
            var maxStart = Math.Min(center, 14 - word.Length + 1);

            for (var start = minStart; start <= maxStart; start++)
            {
                var startRow = horizontal ? center : start;
                var startCol = horizontal ? start : center;
                TryAddCandidate(candidates, board, rack, word, startRow, startCol, horizontal, isFirstMove: true);
            }
        }
    }

    private void AddConnectedCandidates(
        Dictionary<string, MoveSuggestion> candidates,
        BoardState board,
        IReadOnlyList<char> rack,
        string word)
    {
        for (var row = 0; row < 15; row++)
        for (var col = 0; col < 15; col++)
        {
            var tile = board.Grid[row, col];
            if (tile is null)
                continue;

            var anchorLetter = char.ToUpperInvariant(tile.Letter);

            for (var i = 0; i < word.Length; i++)
            {
                if (word[i] != anchorLetter)
                    continue;

                var startCol = col - i;
                TryAddCandidate(candidates, board, rack, word, row, startCol, isHorizontal: true, isFirstMove: false);

                var startRow = row - i;
                TryAddCandidate(candidates, board, rack, word, startRow, col, isHorizontal: false, isFirstMove: false);
            }
        }
    }

    private void TryAddCandidate(
        Dictionary<string, MoveSuggestion> candidates,
        BoardState board,
        IReadOnlyList<char> rack,
        string word,
        int startRow,
        int startCol,
        bool isHorizontal,
        bool isFirstMove)
    {
        if (!IsInBounds(startRow, startCol, isHorizontal, word.Length))
            return;

        var placements = new Dictionary<Position, char>(word.Length);
        var newPlacements = new Dictionary<Position, char>();

        for (var i = 0; i < word.Length; i++)
        {
            var row = isHorizontal ? startRow : startRow + i;
            var col = isHorizontal ? startCol + i : startCol;

            var pos = new Position(row, col);
            var letter = word[i];
            placements[pos] = letter;

            var existing = board.Grid[row, col];
            if (existing is null)
            {
                newPlacements[pos] = letter;
                continue;
            }

            if (char.ToUpperInvariant(existing.Letter) != letter)
                return;
        }

        if (newPlacements.Count == 0)
            return;

        if (!TryAssignWildcards(newPlacements, rack, out var wildcardPositions))
            return;

        var validation = _moveValidator.Validate(placements, board, isFirstMove);
        if (!validation.IsValid)
            return;

        var score = _scoreCalculator.Calculate(placements, board, wildcardPositions);
        var candidate = new MoveSuggestion(
            Word: word,
            Placements: placements,
            Score: score,
            Length: word.Length,
            HeuristicScore: score);

        var key = BuildCandidateKey(candidate);
        candidates.TryAdd(key, candidate);
    }

    private static string BuildCandidateKey(MoveSuggestion suggestion)
    {
        var coords = suggestion.Placements
            .OrderBy(kv => kv.Key.Row)
            .ThenBy(kv => kv.Key.Column)
            .Select(kv => $"{kv.Key.Row}:{kv.Key.Column}:{kv.Value}");
        return $"{suggestion.Word}|{string.Join(";", coords)}";
    }

    private static bool IsInBounds(int startRow, int startCol, bool isHorizontal, int length)
    {
        if (startRow < 0 || startCol < 0 || startRow > 14 || startCol > 14)
            return false;

        var endRow = isHorizontal ? startRow : startRow + length - 1;
        var endCol = isHorizontal ? startCol + length - 1 : startCol;
        return endRow is >= 0 and < 15 && endCol is >= 0 and < 15;
    }

    private static bool IsAsciiWord(string word) =>
        word.All(c => c is >= 'A' and <= 'Z');

    private static bool WordContainsAnyAnchorLetter(string word, IReadOnlySet<char> anchors)
        => word.Any(anchors.Contains);

    private static HashSet<char> GetBoardAnchorLetters(BoardState board)
    {
        var letters = new HashSet<char>();
        for (var row = 0; row < 15; row++)
            for (var col = 0; col < 15; col++)
            {
                var tile = board.Grid[row, col];
                if (tile is not null)
                    letters.Add(char.ToUpperInvariant(tile.Letter));
            }

        return letters;
    }

    private static bool TryAssignWildcards(
        IReadOnlyDictionary<Position, char> lettersToPlace,
        IReadOnlyList<char> rack,
        out HashSet<Position> wildcardPositions)
    {
        wildcardPositions = [];

        var available = rack
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());

        available.TryGetValue('*', out var wildcards);

        foreach (var (pos, letter) in lettersToPlace)
        {
            if (available.TryGetValue(letter, out var count) && count > 0)
            {
                available[letter] = count - 1;
                continue;
            }

            if (wildcards <= 0)
                return false;

            wildcards--;
            wildcardPositions.Add(pos);
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

