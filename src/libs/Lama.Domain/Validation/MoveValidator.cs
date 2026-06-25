using Lama.Contracts;

namespace Lama.Domain.Validation;

/// <summary>
/// Valide un coup selon les règles officielles Scrabble.
///
/// Règles vérifiées :
/// 1. Le coup doit avoir au moins une lettre.
/// 2. Toutes les lettres doivent être sur la même ligne ou la même colonne (alignement).
/// 3. Les croisements doivent avoir la même lettre que celle existante.
/// 4. Au moins une lettre doit être nouvellement posée.
/// 5. Pas de trou dans le mot.
/// 6. Premier coup : le mot doit passer par la case centrale H8 (7, 7).
/// 7. Hors premier coup : connexion obligatoire à une tuile existante.
/// 8. Le mot formé doit avoir au moins 2 lettres.
/// 9. Tous les mots formés (principal + croisements) doivent être dans le dictionnaire.
/// </summary>
public sealed class MoveValidator
{
    private static readonly Position Center = new(7, 7);

    private readonly IReadOnlySet<string> _dictionary;

    public MoveValidator(IReadOnlySet<string> dictionary)
    {
        _dictionary = dictionary;
    }

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
                    $"Lettre invalide '{letter}'. Utilisez A-Z ou '*' pour un joker.");

            var existingTile = board.Grid[pos.Row, pos.Column];
            if (existingTile is not null)
            {
                var existingLetter = char.ToUpperInvariant(existingTile.Letter);
                var proposedLetter = char.ToUpperInvariant(letter);
                if (existingLetter != proposedLetter)
                    return MoveValidationResult.Invalid(
                        $"À la case {FormatPosition(pos)}, la lettre '{existingLetter}' existe déjà. " +
                        $"Vous tentez de placer '{proposedLetter}'. " +
                        $"Pour un croisement valide, les lettres doivent être identiques.");
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

        // 4. Alignement
        var rows = placements.Keys.Select(p => p.Row).Distinct().ToList();
        var cols = placements.Keys.Select(p => p.Column).Distinct().ToList();
        var isHorizontal = rows.Count == 1;
        var isVertical   = cols.Count == 1;

        if (!isHorizontal && !isVertical)
            return MoveValidationResult.Invalid(
                "Toutes les lettres doivent être sur la même ligne ou la même colonne.");

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
            if (!_dictionary.Contains(firstWord))
                return MoveValidationResult.Invalid(
                    $"« {firstWord} » n'est pas dans le dictionnaire.");

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
        if (!_dictionary.Contains(mainWord))
            return MoveValidationResult.Invalid(
                $"« {mainWord} » n'est pas dans le dictionnaire.");

        // 10. Dictionnaire — mots croisés
        var crossWords = ExtractCrossWords(placements, board, isHorizontal);
        foreach (var crossWord in crossWords)
        {
            if (!_dictionary.Contains(crossWord))
                return MoveValidationResult.Invalid(
                    $"Le croisement « {crossWord} » n'est pas dans le dictionnaire.");
        }

        return MoveValidationResult.Valid();
    }

    // ── Extraction des mots ──────────────────────────────────────────────────

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
            // Ignorer les cases déjà occupées (croisements réutilisés)
            if (board.Grid[pos.Row, pos.Column] is not null) continue;

            string crossWord;
            if (mainIsHorizontal)
            {
                // Croisement vertical
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
                // Croisement horizontal
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

    // ── Helpers privés ────────────────────────────────────────────────────────

    private static bool HasAdjacentTile(
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
        return $"{col}{row}";
    }

    private static bool IsAllowedLetter(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        return (upper >= 'A' && upper <= 'Z') || upper == '*';
    }
}
