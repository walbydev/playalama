using Lama.Contracts;
using Lama.Domain.Board;
using Lama.Domain.Validation;

namespace Lama.Domain.Scoring;

/// <summary>
/// Analyse un coup pour valider sa conformité aux règles et calculer son score.
/// Fusionne les responsabilités de validation et de calcul de score
/// pour éviter la duplication de logique (extraction des mots, croisements).
/// </summary>
public sealed class MoveAnalyzer
{
    private const int ScrabbleBonus      = 50;
    private const int ScrabbleBonusTiles = 7;
    private static readonly Position Center = new(7, 7);

    private readonly IReadOnlySet<string> _dictionary;
    private readonly IReadOnlyDictionary<char, int> _letterScores;
    private readonly bool _lenientMode;

    public MoveAnalyzer(
        IReadOnlySet<string> dictionary,
        IReadOnlyDictionary<char, int> letterScores)
    {
        _dictionary = dictionary;
        _letterScores = letterScores;
        _lenientMode = dictionary.Count == 0;
    }

    // ── Validation ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valide un coup selon les règles officielles Scrabble.
    /// </summary>
    public MoveValidationResult Validate(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        bool isFirstMove)
    {
        // 1. Coup vide
        if (placements.Count == 0)
            return MoveValidationResult.Invalid("Un coup doit contenir au moins une lettre.");

        // 2. Lettres valides + comptage nouvelles cases
        var newLetterCount = 0;
        foreach (var (pos, letter) in placements)
        {
            if (!IsAllowedLetter(letter))
                return MoveValidationResult.Invalid(
                    "Lettre invalide '" + letter + "'. Utilisez A-Z ou '*' pour un joker.");

            var existingTile = board.Grid[pos.Row, pos.Column];
            if (existingTile is not null)
            {
                var existingLetter = char.ToUpperInvariant(existingTile.Letter);
                var proposedLetter = char.ToUpperInvariant(letter);
                if (existingLetter != proposedLetter)
                    return MoveValidationResult.Invalid(
                        "À la case " + FormatPosition(pos) + ", la lettre '" + existingLetter
                        + "' existe déjà. Vous tentez de placer '" + proposedLetter
                        + "'. Pour un croisement valide, les lettres doivent être identiques.");
            }
            else
            {
                newLetterCount++;
            }
        }

        // 3. Au moins une lettre nouvellement posée
        if (newLetterCount == 0)
            return MoveValidationResult.Invalid(
                "Au moins une lettre doit être nouvellement posée.");

        // 4. Alignement + direction
        var rows = placements.Keys.Select(p => p.Row).Distinct().ToList();
        var cols = placements.Keys.Select(p => p.Column).Distinct().ToList();

        if (rows.Count > 1 && cols.Count > 1)
            return MoveValidationResult.Invalid(
                "Toutes les lettres doivent être sur la même ligne ou la même colonne.");

        var isHorizontal = DetermineIsHorizontal(placements, board);

        // 5. Pas de trou
        if (isHorizontal)
        {
            var row = rows[0];
            var minCol = cols.Min();
            var maxCol = cols.Max();
            for (var c = minCol; c <= maxCol; c++)
            {
                var pos = new Position(row, c);
                if (!placements.ContainsKey(pos) && board.Grid[pos.Row, pos.Column] is null)
                    return MoveValidationResult.Invalid(
                        "Il y a un trou dans le mot : toutes les cases intermédiaires doivent être occupées ou posées.");
            }
        }
        else
        {
            var col = cols[0];
            var minRow = rows.Min();
            var maxRow = rows.Max();
            for (var r = minRow; r <= maxRow; r++)
            {
                var pos = new Position(r, col);
                if (!placements.ContainsKey(pos) && board.Grid[pos.Row, pos.Column] is null)
                    return MoveValidationResult.Invalid(
                        "Il y a un trou dans le mot : toutes les cases intermédiaires doivent être occupées ou posées.");
            }
        }

        // 6. Premier coup : passe par H8, ≥2 lettres, dictionnaire
        if (isFirstMove)
        {
            if (!placements.ContainsKey(Center))
                return MoveValidationResult.Invalid(
                    "Le premier mot doit passer par la case centrale H8 (colonne H, ligne 8).");
            if (placements.Count < 2)
                return MoveValidationResult.Invalid("Le premier mot doit avoir au moins 2 lettres.");

            var firstWord = ExtractMainWord(placements, board, isHorizontal);
            if (!_lenientMode && !_dictionary.Contains(firstWord))
                return MoveValidationResult.Invalid(
                    "« " + firstWord + " » n'est pas dans le dictionnaire.");

            return MoveValidationResult.Valid();
        }

        // 7. Connexion
        var isConnected = placements.Keys.Any(pos => HasAdjacentTile(pos, board, placements));
        if (!isConnected)
            return MoveValidationResult.Invalid(
                "Le mot doit être connecté aux lettres déjà présentes sur le plateau.");

        // 8. Longueur ≥ 2
        var mainWord = ExtractMainWord(placements, board, isHorizontal);
        if (mainWord.Length < 2)
            return MoveValidationResult.Invalid("Le mot formé doit avoir au moins 2 lettres.");

        // 9. Dictionnaire — mot principal
        if (!_lenientMode && !_dictionary.Contains(mainWord))
            return MoveValidationResult.Invalid(
                "« " + mainWord + " » n'est pas dans le dictionnaire.");

        // 10. Dictionnaire — mots croisés
        var crossWords = ExtractCrossWords(placements, board, isHorizontal);
        foreach (var crossWord in crossWords)
        {
            if (!_lenientMode && !_dictionary.Contains(crossWord))
                return MoveValidationResult.Invalid(
                    "Le croisement « " + crossWord + " » n'est pas dans le dictionnaire.");
        }

        return MoveValidationResult.Valid();
    }

    // ── Calcul de score ───────────────────────────────────────────────────────────

    /// <summary>Calcule le score du mot principal uniquement (sans mots croisés ni bonus Scrabble).</summary>
    public int Calculate(IReadOnlyDictionary<Position, char> placements, BoardState board)
        => Calculate(placements, board, wildcardPositions: null);

    /// <summary>
    /// Calcule le score du mot principal en tenant compte des jokers.
    /// Inclut le bonus Scrabble si 7 lettres ou plus sont posées.
    /// </summary>
    public int Calculate(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions)
    {
        if (placements.Count == 0) return 0;
        var score = CalculateWordScore(placements, board, wildcardPositions, out var newlyPlaced);
        if (newlyPlaced >= ScrabbleBonusTiles) score += ScrabbleBonus;
        return score;
    }

    /// <summary>
    /// Calcule le score total d'un coup : mot principal + tous les mots croisés + bonus Scrabble.
    /// </summary>
    public int CalculateTotal(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions,
        bool isHorizontal)
    {
        if (placements.Count == 0) return 0;

        var mainScore = CalculateWordScore(placements, board, wildcardPositions, out var newlyPlacedTiles);
        var crossScore = CalculateCrossWordsScore(placements, board, wildcardPositions, isHorizontal);

        var total = mainScore + crossScore;
        if (newlyPlacedTiles >= ScrabbleBonusTiles)
            total += ScrabbleBonus;

        return total;
    }

    // ── Extraction des mots ───────────────────────────────────────────────────────

    /// <summary>
    /// Détermine si le coup est horizontal, en tenant compte du contexte du plateau
    /// pour les placements d'une seule tuile.
    /// </summary>
    public static bool DetermineIsHorizontal(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board)
    {
        var rowCount = placements.Keys.Select(p => p.Row).Distinct().Count();
        var colCount = placements.Keys.Select(p => p.Column).Distinct().Count();

        if (rowCount > 1) return false;
        if (colCount > 1) return true;

        var pos = placements.Keys.First();
        var hasHorizontalNeighbor =
            (pos.Column > 0  && board.Grid[pos.Row, pos.Column - 1] is not null) ||
            (pos.Column < 14 && board.Grid[pos.Row, pos.Column + 1] is not null);

        return hasHorizontalNeighbor;
    }

    /// <summary>
    /// Extrait le mot principal formé par le coup (lettres posées + lettres existantes contiguës).
    /// </summary>
    public string ExtractMainWord(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        bool isHorizontal)
    {
        var positions = placements.Keys.ToList();
        var sb = new System.Text.StringBuilder();

        if (isHorizontal)
        {
            var row = positions[0].Row;
            var minCol = positions.Min(p => p.Column);
            var maxCol = positions.Max(p => p.Column);
            while (minCol > 0 && board.Grid[row, minCol - 1] is not null) minCol--;
            while (maxCol < 14 && board.Grid[row, maxCol + 1] is not null) maxCol++;
            for (var c = minCol; c <= maxCol; c++)
            {
                var pos = new Position(row, c);
                var letter = placements.TryGetValue(pos, out var pl) ? pl : board.Grid[row, c]!.Letter;
                sb.Append(char.ToUpperInvariant(letter));
            }
        }
        else
        {
            var col = positions[0].Column;
            var minRow = positions.Min(p => p.Row);
            var maxRow = positions.Max(p => p.Row);
            while (minRow > 0 && board.Grid[minRow - 1, col] is not null) minRow--;
            while (maxRow < 14 && board.Grid[maxRow + 1, col] is not null) maxRow++;
            for (var r = minRow; r <= maxRow; r++)
            {
                var pos = new Position(r, col);
                var letter = placements.TryGetValue(pos, out var pl) ? pl : board.Grid[r, col]!.Letter;
                sb.Append(char.ToUpperInvariant(letter));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extrait les mots croisés (perpendiculaires) formés par chaque nouvelle tuile posée.
    /// Seuls les mots de longueur ≥ 2 sont retournés.
    /// </summary>
    public List<string> ExtractCrossWords(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        bool mainIsHorizontal)
    {
        var crossWords = new List<string>();

        foreach (var (pos, letter) in placements)
        {
            if (board.Grid[pos.Row, pos.Column] is not null) continue;

            string crossWord;
            if (mainIsHorizontal)
            {
                var minRow = pos.Row;
                var maxRow = pos.Row;
                while (minRow > 0 && board.Grid[minRow - 1, pos.Column] is not null) minRow--;
                while (maxRow < 14 && board.Grid[maxRow + 1, pos.Column] is not null) maxRow++;
                if (maxRow == minRow) continue;

                var sb = new System.Text.StringBuilder();
                for (var r = minRow; r <= maxRow; r++)
                {
                    var posR = new Position(r, pos.Column);
                    var l = placements.TryGetValue(posR, out var pl) ? pl : board.Grid[r, pos.Column]!.Letter;
                    sb.Append(char.ToUpperInvariant(l));
                }
                crossWord = sb.ToString();
            }
            else
            {
                var minCol = pos.Column;
                var maxCol = pos.Column;
                while (minCol > 0 && board.Grid[pos.Row, minCol - 1] is not null) minCol--;
                while (maxCol < 14 && board.Grid[pos.Row, maxCol + 1] is not null) maxCol++;
                if (maxCol == minCol) continue;

                var sb = new System.Text.StringBuilder();
                for (var c = minCol; c <= maxCol; c++)
                {
                    var posC = new Position(pos.Row, c);
                    var l = placements.TryGetValue(posC, out var pl) ? pl : board.Grid[pos.Row, c]!.Letter;
                    sb.Append(char.ToUpperInvariant(l));
                }
                crossWord = sb.ToString();
            }

            if (crossWord.Length >= 2)
                crossWords.Add(crossWord);
        }

        return crossWords;
    }

    // ── Helpers privés (validation) ───────────────────────────────────────────────

    private bool HasAdjacentTile(
        Position pos,
        BoardState board,
        IReadOnlyDictionary<Position, char> placements)
    {
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

    private static string FormatPosition(Position pos)
    {
        var col = (char)('A' + pos.Column);
        var row = pos.Row + 1;
        return col.ToString() + row;
    }

    private static bool IsAllowedLetter(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        return (upper >= 'A' && upper <= 'Z') || upper == '*';
    }

    // ── Helpers privés (calcul de score) ──────────────────────────────────────────

    private int CalculateWordScore(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions,
        out int newlyPlacedTiles)
    {
        var wordScore      = 0;
        var wordMultiplier = 1;
        newlyPlacedTiles   = 0;

        foreach (var (pos, letter) in placements)
        {
            var isWildcard  = wildcardPositions?.Contains(pos) == true;
            var letterValue = isWildcard ? 0 : GetLetterValue(letter);
            var bonus       = BonusMap.GetBonus(pos);
            var existingTile = board.Grid[pos.Row, pos.Column];
            var isNewSquare  = existingTile is null;

            if (isNewSquare)
            {
                newlyPlacedTiles++;
                wordScore      += letterValue * bonus.LetterMultiplier;
                wordMultiplier *= bonus.WordMultiplier;
            }
            else
            {
                var existingLetterValue = existingTile!.IsWildcard
                    ? 0
                    : GetLetterValue(existingTile.Letter);
                wordScore += existingLetterValue;
            }
        }

        return wordScore * wordMultiplier;
    }

    private int CalculateCrossWordsScore(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions,
        bool mainIsHorizontal)
    {
        var total = 0;

        foreach (var (pos, letter) in placements)
        {
            if (board.Grid[pos.Row, pos.Column] is not null) continue;

            int crossScore = 0;
            int crossMulti = 1;
            bool hasCross  = false;

            if (mainIsHorizontal)
            {
                var minRow = pos.Row;
                var maxRow = pos.Row;
                while (minRow > 0 && board.Grid[minRow - 1, pos.Column] is not null) minRow--;
                while (maxRow < 14 && board.Grid[maxRow + 1, pos.Column] is not null) maxRow++;
                if (maxRow == minRow) continue;

                hasCross = true;
                for (var r = minRow; r <= maxRow; r++)
                {
                    var tilePos = new Position(r, pos.Column);
                    var isNewPos = board.Grid[r, pos.Column] is null;
                    var bonus    = BonusMap.GetBonus(tilePos);

                    if (isNewPos)
                    {
                        var isWildcard = wildcardPositions?.Contains(tilePos) == true;
                        var letterVal  = isWildcard ? 0 : GetLetterValue(letter);
                        crossScore    += letterVal * bonus.LetterMultiplier;
                        crossMulti    *= bonus.WordMultiplier;
                    }
                    else
                    {
                        var existingTile = board.Grid[r, pos.Column]!;
                        crossScore += existingTile.IsWildcard ? 0 : GetLetterValue(existingTile.Letter);
                    }
                }
            }
            else
            {
                var minCol = pos.Column;
                var maxCol = pos.Column;
                while (minCol > 0 && board.Grid[pos.Row, minCol - 1] is not null) minCol--;
                while (maxCol < 14 && board.Grid[pos.Row, maxCol + 1] is not null) maxCol++;
                if (maxCol == minCol) continue;

                hasCross = true;
                for (var c = minCol; c <= maxCol; c++)
                {
                    var tilePos = new Position(pos.Row, c);
                    var isNewPos = board.Grid[pos.Row, c] is null;
                    var bonus    = BonusMap.GetBonus(tilePos);

                    if (isNewPos)
                    {
                        var isWildcard = wildcardPositions?.Contains(tilePos) == true;
                        var letterVal  = isWildcard ? 0 : GetLetterValue(letter);
                        crossScore    += letterVal * bonus.LetterMultiplier;
                        crossMulti    *= bonus.WordMultiplier;
                    }
                    else
                    {
                        var existingTile = board.Grid[pos.Row, c]!;
                        crossScore += existingTile.IsWildcard ? 0 : GetLetterValue(existingTile.Letter);
                    }
                }
            }

            if (hasCross)
                total += crossScore * crossMulti;
        }

        return total;
    }

    private int GetLetterValue(char letter) =>
        _letterScores.TryGetValue(char.ToUpperInvariant(letter), out var value) ? value : 0;
}
