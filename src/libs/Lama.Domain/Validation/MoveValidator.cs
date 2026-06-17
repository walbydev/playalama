using Lama.Contracts;

namespace Lama.Domain.Validation;

/// <summary>
/// Valide un coup selon les règles officielles Scrabble.
///
/// Règles vérifiées :
/// 1. Le coup ne doit pas être vide.
/// 2. Toutes les lettres doivent être sur la même ligne ou la même colonne (alignement).
/// 3. Les cases ciblées ne doivent pas déjà être occupées.
/// 4. Entre la première et la dernière lettre posée, toutes les cases intermédiaires
///    doivent être soit posées maintenant, soit déjà occupées (pas de trou).
/// 5. Premier coup : le mot doit passer par la case centrale H8 (7, 7).
/// 6. Hors premier coup : le mot doit être adjacent (connexion) à au moins une tuile existante.
/// 7. Le mot formé doit avoir au moins 2 lettres (posées + existantes contiguës).
/// </summary>
public sealed class MoveValidator
{
    private static readonly Position Center = new(7, 7);

    private readonly IReadOnlySet<string> _dictionary;

    /// <summary>
    /// Initialise le validateur avec le dictionnaire de la langue.
    /// </summary>
    public MoveValidator(IReadOnlySet<string> dictionary)
    {
        _dictionary = dictionary;
    }

    /// <summary>
    /// Valide un coup.
    /// </summary>
    /// <param name="placements">Les lettres nouvellement posées (Position → lettre).</param>
    /// <param name="board">L'état du plateau avant ce coup.</param>
    /// <param name="isFirstMove">True si c'est le premier coup de la partie.</param>
    /// <returns>Le résultat de validation.</returns>
    public MoveValidationResult Validate(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        bool isFirstMove)
    {
        // 1. Coup vide
        if (placements.Count == 0)
            return MoveValidationResult.Invalid("Un coup doit contenir au moins une lettre.");

        // 2. Aucune case ne doit déjà être occupée
        foreach (var (pos, letter) in placements)
        {
            if (!IsAllowedLetter(letter))
                return MoveValidationResult.Invalid(
                    $"Lettre invalide '{letter}'. Utilisez A-Z ou '*' pour un joker.");

            if (board.Grid[pos.Row, pos.Column] is not null)
                return MoveValidationResult.Invalid(
                    $"La case ({pos.Row},{pos.Column}) est déjà occupée.");
        }

        // 3. Alignement : toutes sur la même ligne ou la même colonne
        var rows = placements.Keys.Select(p => p.Row).Distinct().ToList();
        var cols = placements.Keys.Select(p => p.Column).Distinct().ToList();

        var isHorizontal = rows.Count == 1;
        var isVertical   = cols.Count == 1;

        if (!isHorizontal && !isVertical)
            return MoveValidationResult.Invalid(
                "Toutes les lettres doivent être sur la même ligne ou la même colonne.");

        // 4. Pas de trou dans le mot (les cases intermédiaires doivent être occupées ou posées)
        if (isHorizontal)
        {
            var row    = rows[0];
            var minCol = cols.Min();
            var maxCol = cols.Max();
            for (var c = minCol; c <= maxCol; c++)
            {
                var pos = new Position(row, c);
                if (!placements.ContainsKey(pos) && board.Grid[pos.Row, pos.Column] is null)
                    return MoveValidationResult.Invalid(
                        "Il y a un trou dans le mot : toutes les cases intermédiaires " +
                        "doivent être occupées ou posées dans ce coup.");
            }
        }
        else
        {
            var col    = cols[0];
            var minRow = rows.Min();
            var maxRow = rows.Max();
            for (var r = minRow; r <= maxRow; r++)
            {
                var pos = new Position(r, col);
                if (!placements.ContainsKey(pos) && board.Grid[pos.Row, pos.Column] is null)
                    return MoveValidationResult.Invalid(
                        "Il y a un trou dans le mot : toutes les cases intermédiaires " +
                        "doivent être occupées ou posées dans ce coup.");
            }
        }

        // 5. Premier coup : doit passer par H8
        if (isFirstMove)
        {
            if (!placements.ContainsKey(Center))
                return MoveValidationResult.Invalid(
                    "Le premier mot doit passer par la case centrale H8 (colonne H, ligne 8).");

            // Longueur minimale : au moins 2 lettres posées
            if (placements.Count < 2)
                return MoveValidationResult.Invalid(
                    "Le premier mot doit avoir au moins 2 lettres.");

            return MoveValidationResult.Valid();
        }

        // 6. Hors premier coup : connexion obligatoire à au moins une tuile existante
        var isConnected = placements.Keys.Any(pos => HasAdjacentTile(pos, board, placements));
        if (!isConnected)
            return MoveValidationResult.Invalid(
                "Le mot doit être connecté aux lettres déjà présentes sur le plateau.");

        // 7. Longueur minimale du mot formé (lettres posées + lettres existantes contiguës)
        var wordLength = CountWordLength(placements, board, isHorizontal);
        if (wordLength < 2)
            return MoveValidationResult.Invalid(
                "Le mot formé doit avoir au moins 2 lettres.");

        return MoveValidationResult.Valid();
    }

    // ── Helpers privés ────────────────────────────────────────────────────────

    /// <summary>
    /// Vérifie si une position a au moins une tuile adjacente (hors du coup en cours).
    /// </summary>
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
            if (placements.ContainsKey(neighbor)) continue; // dans le même coup
            if (board.Grid[neighbor.Row, neighbor.Column] is not null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Compte la longueur du mot formé (lettres posées + lettres existantes contiguës).
    /// </summary>
    private static int CountWordLength(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        bool isHorizontal)
    {
        var positions = placements.Keys.ToList();
        int start, end, fixed_coord;

        if (isHorizontal)
        {
            fixed_coord = positions[0].Row;
            start       = positions.Min(p => p.Column);
            end         = positions.Max(p => p.Column);

            // Étendre vers la gauche
            while (start > 0 && board.Grid[fixed_coord, start - 1] is not null)
                start--;
            // Étendre vers la droite
            while (end < 14 && board.Grid[fixed_coord, end + 1] is not null)
                end++;

            return end - start + 1;
        }
        else
        {
            fixed_coord = positions[0].Column;
            start       = positions.Min(p => p.Row);
            end         = positions.Max(p => p.Row);

            // Étendre vers le haut
            while (start > 0 && board.Grid[start - 1, fixed_coord] is not null)
                start--;
            // Étendre vers le bas
            while (end < 14 && board.Grid[end + 1, fixed_coord] is not null)
                end++;

            return end - start + 1;
        }
    }

    private static bool IsAllowedLetter(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        return (upper >= 'A' && upper <= 'Z') || upper == '*';
    }
}
