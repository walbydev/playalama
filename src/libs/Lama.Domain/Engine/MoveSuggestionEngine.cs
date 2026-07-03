using Lama.Contracts;
using Lama.Domain.Board;
using Lama.Domain.Scoring;

namespace Lama.Domain.Engine;

/// <summary>
/// Moteur de suggestion de coups (stub).
/// Le moteur complet sera ajoute dans une prochaine iteration.
/// </summary>
public sealed class MoveSuggestionEngine
{
    private readonly IReadOnlySet<string> _dictionary;
    private readonly MoveAnalyzer _moveAnalyzer;

    public MoveSuggestionEngine()
        : this(new HashSet<string>(), new Dictionary<char, int>())
    {
    }

    public MoveSuggestionEngine(
        IReadOnlySet<string> dictionary,
        IReadOnlyDictionary<char, int> letterScores)
    {
        _dictionary = dictionary;
        _moveAnalyzer = new MoveAnalyzer(dictionary, letterScores);
    }

    /// <summary>
    /// Propose les meilleurs coups pour le joueur courant.
    /// </summary>
    public IReadOnlyList<MoveSuggestion> SuggestTopMoves(
        GameState gameState,
        Player currentPlayer,
        int top,
        MoveSuggestionStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        if (top <= 0 || _dictionary.Count == 0)
            return [];
        
        var rack = currentPlayer.Rack.Select(char.ToUpperInvariant).ToList();
        var rackInventory = RackInventory.From(rack);
        var isFirstMove = !HasAnyTile(gameState.Board);
        var candidates = new Dictionary<string, MoveSuggestion>(StringComparer.Ordinal);
        var boardAnchorLetters = GetBoardAnchorLetters(gameState.Board);
        var anchorIndex = BuildAnchorIndex(gameState.Board);
        var keepThreshold = Math.Max(top * 8, top + 24);

        foreach (var rawWord in _dictionary)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var word = rawWord.Trim().ToUpperInvariant();
            if (!IsAsciiWord(word) || word.Length < 2 || word.Length > 15)
                continue;

            if (isFirstMove)
            {
                if (word.Length > rack.Count)
                    continue;

                AddFirstMoveCandidates(candidates, gameState.Board, rackInventory, word);
                if (candidates.Count > keepThreshold)
                    PruneCandidatesInPlace(candidates, keepThreshold, strategy);
                continue;
            }

            if (!WordContainsAnyAnchorLetter(word, boardAnchorLetters))
                continue;

            AddConnectedCandidates(candidates, gameState.Board, rackInventory, word, anchorIndex);
            if (candidates.Count > keepThreshold)
                PruneCandidatesInPlace(candidates, keepThreshold, strategy);
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

    private static void PruneCandidatesInPlace(
        Dictionary<string, MoveSuggestion> candidates,
        int keep,
        MoveSuggestionStrategy strategy)
    {
        if (candidates.Count <= keep)
            return;

        var bestKeys = strategy switch
        {
            MoveSuggestionStrategy.Length => candidates
                .OrderByDescending(kv => kv.Value.Length)
                .ThenByDescending(kv => kv.Value.Score)
                .ThenBy(kv => kv.Value.Word, StringComparer.Ordinal)
                .Take(keep)
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.Ordinal),

            MoveSuggestionStrategy.Balanced => candidates
                .OrderByDescending(kv => kv.Value.HeuristicScore)
                .ThenByDescending(kv => kv.Value.Score)
                .ThenBy(kv => kv.Value.Word, StringComparer.Ordinal)
                .Take(keep)
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.Ordinal),

            _ => candidates
                .OrderByDescending(kv => kv.Value.Score)
                .ThenByDescending(kv => kv.Value.Length)
                .ThenBy(kv => kv.Value.Word, StringComparer.Ordinal)
                .Take(keep)
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.Ordinal)
        };

        var toRemove = candidates.Keys
            .Where(k => !bestKeys.Contains(k))
            .ToList();

        foreach (var key in toRemove)
            candidates.Remove(key);
    }

    private void AddFirstMoveCandidates(
        Dictionary<string, MoveSuggestion> candidates,
        BoardState board,
        RackInventory rack,
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
        RackInventory rack,
        string word,
        IReadOnlyDictionary<char, List<Position>> anchorIndex)
    {
        for (var i = 0; i < word.Length; i++)
        {
            var letter = word[i];
            if (!anchorIndex.TryGetValue(letter, out var anchors))
                continue;

            foreach (var anchor in anchors)
            {
                var startCol = anchor.Column - i;
                TryAddCandidate(candidates, board, rack, word, anchor.Row, startCol, isHorizontal: true, isFirstMove: false);

                var startRow = anchor.Row - i;
                TryAddCandidate(candidates, board, rack, word, startRow, anchor.Column, isHorizontal: false, isFirstMove: false);
            }
        }
    }

    private void TryAddCandidate(
        Dictionary<string, MoveSuggestion> candidates,
        BoardState board,
        RackInventory rack,
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

        if (newPlacements.Count > rack.TotalTiles)
            return;

        if (!TryAssignWildcards(newPlacements, rack, out var wildcardPositions))
            return;

        var validation = _moveAnalyzer.Validate(placements, board, isFirstMove);
        if (!validation.IsValid)
            return;

        var score = _moveAnalyzer.Calculate(placements, board, wildcardPositions);
        var heuristic = ComputeBalancedScore(board, placements, score);
        var candidate = new MoveSuggestion(
            Word: word,
            Placements: placements,
            Score: score,
            Length: word.Length,
            HeuristicScore: heuristic);

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

    private static Dictionary<char, List<Position>> BuildAnchorIndex(BoardState board)
    {
        var index = new Dictionary<char, List<Position>>();
        for (var row = 0; row < 15; row++)
            for (var col = 0; col < 15; col++)
            {
                var tile = board.Grid[row, col];
                if (tile is null)
                    continue;

                var key = char.ToUpperInvariant(tile.Letter);
                if (!index.TryGetValue(key, out var positions))
                {
                    positions = [];
                    index[key] = positions;
                }

                positions.Add(new Position(row, col));
            }

        return index;
    }

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
        RackInventory rack,
        out HashSet<Position> wildcardPositions)
    {
        wildcardPositions = [];
        var remaining = rack.CloneCounts();
        var wildcards = rack.Wildcards;

        foreach (var (pos, letter) in lettersToPlace)
        {
            var idx = LetterIndex(letter);
            if (idx >= 0 && remaining[idx] > 0)
            {
                remaining[idx]--;
                continue;
            }

            if (wildcards <= 0)
                return false;

            wildcards--;
            wildcardPositions.Add(pos);
        }

        return true;
    }

    private static int LetterIndex(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        return upper is >= 'A' and <= 'Z' ? upper - 'A' : -1;
    }

    private static bool HasAnyTile(BoardState board)
    {
        for (var row = 0; row < 15; row++)
            for (var col = 0; col < 15; col++)
                if (board.Grid[row, col] is not null)
                    return true;

        return false;
    }

    private static double ComputeBalancedScore(
        BoardState board,
        IReadOnlyDictionary<Position, char> placements,
        int immediateScore)
    {
        var consumedPremium = 0;
        var exposureRisk = 0;

        // Comptage des croisements consécutifs : une nouvelle tuile posée forme un
        // croisement si elle a un voisin perpendiculaire déjà présent sur le plateau.
        // On pénalise les mots créant plus d'un croisement consécutif (ouverture
        // excessive pour l'adversaire).
        var consecutiveCrossings = 0;
        var maxConsecutiveCrossings = 0;

        foreach (var pos in placements.Keys)
        {
            var isNewSquare = board.Grid[pos.Row, pos.Column] is null;

            if (isNewSquare)
            {
                consumedPremium += PremiumWeight(BonusMap.GetBonus(pos).Type);

                // Détecte un croisement perpendiculaire : voisin orthogonal existant
                // qui n'appartient pas au mot posé (= mot perpendiculaire formé).
                var hasCrossing = HasPerpendicularNeighbor(pos, board, placements);
                if (hasCrossing)
                {
                    consecutiveCrossings++;
                    maxConsecutiveCrossings = Math.Max(maxConsecutiveCrossings, consecutiveCrossings);
                }
                else
                {
                    consecutiveCrossings = 0;
                }
            }

            foreach (var neighbor in GetNeighbors(pos))
            {
                if (!neighbor.IsValid)
                    continue;

                if (placements.ContainsKey(neighbor))
                    continue;

                if (board.Grid[neighbor.Row, neighbor.Column] is not null)
                    continue;

                exposureRisk += PremiumWeight(BonusMap.GetBonus(neighbor).Type);
            }
        }

        // Pénalité : au-delà de 1 croisement consécutif, chaque croisement
        // supplémentaire réduit le score heuristique.
        var crossingPenalty = Math.Max(0, maxConsecutiveCrossings - 1) * 1.5;

        // Heuristique : valorise la prise de premium, pénalise l'ouverture de
        // premiums adverses et les croisements consécutifs excessifs.
        return immediateScore + consumedPremium * 0.35 - exposureRisk * 0.20 - crossingPenalty;
    }

    /// <summary>
    /// Indique si une nouvelle tuile à <paramref name="pos"/> forme un croisement
    /// perpendiculaire avec une tuile existante du plateau (i.e. un mot perpendiculaire
    /// est créé, hors prolongement du mot principal).
    /// </summary>
    private static bool HasPerpendicularNeighbor(
        Position pos,
        BoardState board,
        IReadOnlyDictionary<Position, char> placements)
    {
        // Voisins orthogonaux n'appartenant pas aux placements = tuiles existantes
        // qui forment un croisement perpendiculaire.
        Span<Position> neighbors =
        [
            new Position(pos.Row - 1, pos.Column),
            new Position(pos.Row + 1, pos.Column),
            new Position(pos.Row,     pos.Column - 1),
            new Position(pos.Row,     pos.Column + 1)
        ];

        foreach (var neighbor in neighbors)
        {
            if (!neighbor.IsValid) continue;
            if (placements.ContainsKey(neighbor)) continue;
            if (board.Grid[neighbor.Row, neighbor.Column] is not null)
                return true;
        }
        return false;
    }

    private static IEnumerable<Position> GetNeighbors(Position pos)
    {
        yield return new Position(pos.Row - 1, pos.Column);
        yield return new Position(pos.Row + 1, pos.Column);
        yield return new Position(pos.Row, pos.Column - 1);
        yield return new Position(pos.Row, pos.Column + 1);
    }

    private static int PremiumWeight(BonusType type) => type switch
    {
        BonusType.TripleWord => 6,
        BonusType.DoubleWord => 4,
        BonusType.Start => 4,
        BonusType.TripleLetter => 3,
        BonusType.DoubleLetter => 2,
        _ => 0
    };

    private sealed class RackInventory
    {
        private readonly int[] _letterCounts;

        private RackInventory(int[] letterCounts, int wildcards)
        {
            _letterCounts = letterCounts;
            Wildcards = wildcards;
            TotalTiles = _letterCounts.Sum() + wildcards;
        }

        public int Wildcards { get; }
        public int TotalTiles { get; }

        public static RackInventory From(IEnumerable<char> rack)
        {
            var counts = new int[26];
            var wildcards = 0;

            foreach (var letter in rack)
            {
                if (letter == '*')
                {
                    wildcards++;
                    continue;
                }

                var idx = LetterIndex(letter);
                if (idx >= 0)
                    counts[idx]++;
            }

            return new RackInventory(counts, wildcards);
        }

        public int[] CloneCounts() => (int[])_letterCounts.Clone();
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

